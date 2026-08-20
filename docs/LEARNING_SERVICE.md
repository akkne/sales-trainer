# LEARNING_SERVICE.md — Learning Service extraction

> Phase 8 of the [microservices migration](MICROSERVICES_ROADMAP.md). Extracts the
> content tree and the learner's progress through it out of the monolith
> (`src/backend/api`) into an independently deployable `learning-service`. This is the
> last and largest service; after its routes are flipped the monolith serves only the
> `/admin/users/*` admin user-management routes (never extracted; Phase 9 moves them).
> Its code stays in the repo as reference, retired in Phase 9.

## Bounded context

Content + the learner's progress through it:

- **SkillTree** — skills, stages, topics; the `/skill-tree` aggregate view.
- **Lessons / Exercises** — the exercise tree, submission grading, attempts, progress.
- **Reference** — per-skill reference materials.
- **Techniques** — the technique library (cards, detail, coach, user progress).
- **DailyQuotes** — the daily motivational quote.
- **Admin** — content CRUD for everything above, plus the JSON content seeder.

## Layout

```
src/backend/learning-service/
  Learning/
    Program.cs                         service host wiring (extensions only)
    Sellevate.Learning.csproj
    Dockerfile                         build context = src/backend (for building-blocks)
    Common/Constants/                  ExerciseTypes, LessonProgressStatuses, LessonKinds, policies
    DependencyInjection/               AddLearningServices(IServiceCollection, IConfiguration)
    Eventing/                          exercise/lesson/skill/technique producers + user.* replica consumer
    Identity/                          local UserReplica
    Features/
      SkillTree/                       /skills, /skill-tree, /skills/{id}/topics
      Lessons/                         Lesson/Exercise/progress/attempt models + LessonVersion
                                       (40.15: snapshot serializer, canonical JSON, slugs)
      Programs/                        /program, /program/switch + programme versions, items,
                                       enrollments and diffs (40.17)
      Exercises/                       /lessons, /exercises/*, deterministic + AI grading, chat/voice
      Reference/                       /reference
      Techniques/                      /techniques
      DailyQuotes/                     /daily-quote
      Admin/                           /admin/* content CRUD + /admin/seeder/*
    Infrastructure/
      Ai/                              AI evaluation client (calls ai-service /ai/evaluate) + ported chat/TTS
      Configuration/                   AiService + OpenAI/TTS options
      Data/                            LearningDbContext (Postgres) + EF migrations + bootstrapper
  Learning.Tests/                      NUnit unit tests
```

## Data ownership

Owns Postgres database **`learning`** (created on startup by `DatabaseBootstrapper`,
then EF migration `InitialLearningSchema` runs):

`Skills`, `SkillStages`, `Topics`, `UserSkillProgressRecords`, `Lessons`, `LessonVersions`, `Exercises`,
`UserLessonProgressRecords`, `UserExerciseAttempts`, `ExerciseTypePrompts`,
`ReferenceMaterials`, `DailyQuotes`, `Techniques`, `TechniqueSkills`,
`TechniqueCoaches`, `UserTechniqueProgressRecords`, plus a local `UserReplicas`
read-model (`UserId`, `Email`, `DisplayName`, `AvatarKey`) fed by `user.*` events.

Reuses the shared Redis (Kafka idempotency store) and Kafka broker.

## Multi-tenancy (Phase 40.10)

learning-db is the first database where **tenant data and the global content library live side by
side**, so the two halves are modelled differently on purpose.

| | Tables | `OrganizationId` | Query filter | RLS policy |
|---|---|---|---|---|
| **Tenant data** | `UserSkillProgressRecords`, `UserLessonProgressRecords`, `UserExerciseAttempts`, `UserTechniqueProgress`, `ProgramVersions`/`ProgramItems`/`ProgramEnrollments` (40.17) | `NOT NULL`, `ITenantScoped` | `== current` | `EnableTenantRls` |
| **Content** | `Skills`, `Topics`, `Lessons`, `LessonVersions` (40.15), `Exercises`, `Techniques`, `ReferenceMaterials` | nullable — `NULL` = global library | `== null \|\| == current` | `EnableTenantRlsForContent` |
| **Platform-global** | `ExerciseTypePrompts`, `SkillStages`, `DailyQuotes`, `UserReplicas`, `TechniqueSkills`, `TechniqueCoaches`, `OutboxMessages` | none | none | none |

Points that are easy to get wrong and are therefore pinned by tests
(`Learning.Tests/Unit/LearningTenancyModelTests`, `Learning.Tests/Integration/LearningTenantIsolationIntegrationTests`):

- **Every entity declares its own filter.** EF does not inherit query filters through navigations,
  and every read path here composes `Skill → Topic → Lesson → Exercise`. A filter on `Skill` says
  nothing about `Exercise`. A model-walking unit test fails the build if an entity grows an
  `OrganizationId` without a filter.
- **Content uses `null || ==`, never plain equality.** Plain equality would hand every new customer
  an empty skill tree on day one.
- **`Skill.IconicName` is unique per organization**, not globally (`UNIQUE (OrganizationId,
  IconicName)`), plus a partial unique index over the global rows — Postgres treats NULLs in a
  composite unique index as distinct, so the composite index alone would let two global `objections`
  skills exist. Same treatment for `Topic.IconicName` and `Technique.Slug` — and, from 40.15, for
  `Lesson.Slug`.
- **Progress indexes lead with `OrganizationId`** (docs/TENANCY/TENANCY.md §3).

### The read-transaction rule

`TenantConnectionInterceptor` issues `SET LOCAL app.organization_id` when a transaction starts, and
`SET LOCAL` does nothing outside one. EF opens an implicit transaction for every `SaveChangesAsync`,
so writes are covered for free — **a bare `SELECT` is not, and under RLS it silently returns zero
tenant rows.** learning-service therefore has exactly one pattern, in
`Infrastructure/Data/TenantTransactionScope.cs`:

- read-only service method → `TenantTransactionScope.BeginReadAsync(...)` as its first statement
  (rolls back on dispose — it exists to make rows visible, not to persist anything);
- method that also writes → `BeginWriteAsync(...)` plus an explicit `CommitAsync(...)`.

Both are re-entrant, so a nested call is a no-op and the outermost scope owns the transaction. Two
deliberate placements rather than a blanket request-wide transaction:

- `ExerciseService.SubmitExerciseAnswerAsync` scopes the exercise lookup and the write phase
  separately, because the AI evaluation between them is a network call and must never run with a
  Postgres transaction held open;
- `ExerciseDialogService` closes its scope before a single byte of audio is generated, which is why
  the `/voice/stream` endpoint needs no request-wide transaction.

**That gap is now closed (40.18).** Until this block the controllers under `Features/Admin` talked
to the content tables with no scope, which worked only because content was global for the whole of
40.10-40.17 and the content policy admits global rows even with the session variable unset. The
moment an organization owns a row that stops being true, and the failure is silent: the
administrator overrides a technique and then cannot find it. The four content controllers now carry
`[TenantTransaction]`, one filter that wraps the whole action in a scope, rather than a scope in each
of twenty actions — because the failure mode of the per-action version is somebody adding action
twenty-one.

### Content authoring and the seeder

All content write endpoints were `RequirePlatformAdmin` from 40.10 to 40.17, so content was authored
only by platform staff and `OrganizationId` stayed `NULL`. The seeder and the bundle importer stay
platform-only in 40.18 and 40.19.

**"The seeder needs no special mode" stopped being true in 40.18, and 40.19 fixed it.** Its *writes*
were always fine — it never set an organization. Its *reads* were not: they went through the tenancy
query filter, which admits "global or mine". A platform administrator who is also a member of an
organization loaded that organization's override lessons alongside the base ones, and lessons upsert
on `(topicId, title)` — so re-running a bundle import silently overwrote a customer's edited lesson
and its exercises with the base text, with nothing in the response to say so. The `*/export`
endpoints had the mirror bug. Since 40.19 every read in `AdminSeederController` is narrowed to
`OrganizationId IS NULL` by query (not by role, so it holds whatever tenant header the request
carried), every created row states that owner explicitly, and every import requires an explicit
`target=global` field. See [SEEDER.md](SEEDER.md) §0.

Since 40.18 the four content controllers carry `RequireOrgAdmin` **plus** `ContentAuthoringGuard`,
which is the rule that decides who may write which row: a row with an owning organization belongs to
that organization and RLS has already proved the caller is inside it; a row with a null owner is the
shared library and needs platform rights. Creating content from nothing — a new lesson under a topic,
a new technique, a technique import, a new reference material — stays platform-only, because an
organization customizes what exists and originating an original curriculum is 40.19/40.20's question.

That guard cannot be a row-level-security policy, and it is worth knowing why rather than assuming it
was laziness. The content policy is `OrganizationId IS NULL OR = current` in the `WITH CHECK` clause
as well as the `USING` clause, because a customer must be able to read the shared library. Read as a
write rule it says: any organization may write a row with a null owner, i.e. edit every other
customer's curriculum. The database cannot separate those two cases, because "global" is a null and
not a tenant. What the database *can* enforce is the other half, and it does:
`CK_Lessons_OverrideHasOwner`, `CK_Techniques_OverrideHasOwner` and
`CK_ReferenceMaterials_OverrideHasOwner` say a row with a parent always has an owner.

Phase 40.15 adds the first exception, and it is deliberate. `AdminLessonVersionsController` carries
`RequireOrgAdmin` rather than `RequireSuperAdmin`, because lesson versions are the first content an
organization will author for itself. The policy alone would be a hole — an organization
administrator publishing into a lesson with `OrganizationId IS NULL` would be editing the curriculum
of every other customer — so both write routes check the lesson's owner and require platform rights
for global content. The other direction needs no check: another organization's lessons are already
invisible through the query filter and the RLS policy.

### Immutable lesson versioning (Phase 40.15)

`Lessons` becomes a lifeline rather than a leaf (`ParentLessonId`, `Slug`, `IsArchived`) and
`LessonVersions` holds the snapshots. Full schema in [DB_SCHEMA.md](DB_SCHEMA.md), authoring
walkthrough in [SKILLS_AND_EXERCISES.md](SKILLS_AND_EXERCISES.md), design in
[TENANCY/CONTENT_MODEL.md](TENANCY/CONTENT_MODEL.md) §2. What matters at the service level:

- **The versioned unit is the whole lesson plus its ordered exercises**, snapshotted as one canonical
  JSON document. A `Lessons` row has no body — only a title — so versioning the row alone versions
  nothing, and versioning each `Exercise` separately turns every historical question into a
  reconstruction from N rows.
- **`Exercise` rows stay the working representation.** `LessonVersionService` re-derives the draft's
  snapshot from the live rows on every call, so a draft cannot drift from what the admin is looking
  at. The draft row exists for what the working rows cannot carry: who started editing, when, which
  base version was forked, and the fact that unpublished changes exist — the last of which is what
  40.18's stale-override queue reads.
- **Three guarantees live in Postgres, not in C#:** the freeze trigger
  (`LessonVersions_reject_frozen_change`), the one-draft-per-lesson partial unique index, and the two
  check constraints. Application-level versions of the first two would be a promise and a lost race
  respectively.
- **Every service method opens a `TenantTransactionScope` first**, per the read-transaction rule
  below — `LessonVersions` is an RLS table and a bare `SELECT` outside a transaction sees only global
  rows.
- **Not in this block:** programme versioning and enrollments (40.17), creating overrides and the
  staleness queue (40.18). `ParentLessonId` and `BaseVersionId` exist and are filled correctly when
  set, so those blocks add behaviour rather than schema.

### Progress bound to a version (Phase 40.16)

`UserExerciseAttempt` and `UserLessonProgress` carry `LessonVersionId`
([CONTENT_MODEL.md](TENANCY/CONTENT_MODEL.md) §2.3). The exercise's identity **within** that version
is the `exerciseId` key already inside the snapshot's `Content`, so `ExerciseId` stays as it is and
changes meaning rather than shape: it is a key into a frozen document, not a pointer at an editable
row. What this buys is the one thing the schema could not previously promise — an administrator
fixing a wrong correct-answer no longer moves numbers that were computed months ago.

- **The version is resolved on submission**, not at read time.
  `ILessonVersionService.EnsurePublishedVersionIdAsync` returns the newest published version and,
  when the lesson has never been published at all, mints a version 1 from the live rows
  (`IsBreaking = false`, no author — it records content as it already was, which is not a change and
  belongs to nobody). It runs **before** `SubmitExerciseAnswerAsync` opens its write scope, because
  losing the mint race to another learner raises a unique violation and a unique violation aborts the
  whole transaction it happens in.
- **It does not mint on unpublished drift.** An administrator who edits an exercise and does not
  publish has not made the edit historically visible, and minting on their behalf would stamp every
  such edit — a fixed comma included — as an unattributed content change, splitting the accuracy
  series on cosmetics. That is the failure `is_breaking` exists to prevent, so the gap is left open
  deliberately and recorded in `docs/DECISIONS.md`.
- **`UserLessonProgress.LessonVersionId` is refreshed only when the row advances** — a new best score
  or the transition to completed. Stamping it on every submission would relabel a completion earned
  on version 1 as a completion of version 3.
- **`is_breaking` finally has a reader.** `GET /admin/lessons/{lessonId}/accuracy`
  (`AdminLessonMetricsController` → `LessonAccuracyService`) groups the lesson's published and
  archived versions into segments: a new segment starts at version 1 and at every version published
  as breaking, and cosmetic versions extend the segment they follow. Attempts with no version at all
  are reported as their own `unversionedAttempts` bucket rather than folded into version 1 — nobody
  can prove what those answers were scored against, and merging them silently would be the same lie
  this phase exists to stop, told by the fix instead of by the bug.
- **The historical migration is split in two on purpose.** `LessonVersionBackfill` runs at startup in
  system mode and gives every lesson that has never been published its version 1; it has to be C#,
  because `ContentHash` is a SHA-256 over the exact bytes `LessonSnapshotSerializer` emits and
  Postgres orders `jsonb` keys differently, so a snapshot built in SQL would carry a hash the service
  never reproduces and the next publish would mint a duplicate version.
  `docs/TENANCY/sql/40.16_progress_version_backfill.sql` then binds the existing attempts and
  progress rows to it, in batches, run by a human.
- **Analytics does not compute any of this.** See [ANALYTICS_SERVICE.md](ANALYTICS_SERVICE.md):
  analytics-service is Redis-only, stores no attempts, and its `exercise.completed` counter is a
  platform-wide funnel number with no lesson, no version and no organization in it.

### Programme versioning and enrollment (Phase 40.17)

`ProgramVersions` / `ProgramItems` / `ProgramEnrollments`
([CONTENT_MODEL.md](TENANCY/CONTENT_MODEL.md) §2.5). 40.15 gave a lesson a history and 40.16 bound
progress to it; this does the same one level up, for the curriculum.

They are the first tables in this service that are **strict tenant data and content-adjacent at the
same time**, and the distinction is worth stating because the rest of Stage D went the other way: a
curriculum is a decision one organization made about its own people, so there is no global programme,
`OrganizationId` is `NOT NULL`, and the policy is plain equality rather than `IS NULL OR = current`.

- **A programme is references, and nothing else.** A `ProgramItem` is a skill id, an order index and
  a pinned `LessonVersionId`. Not one write in `ProgramVersionService` touches `Lessons`, `Exercises`
  or `LessonVersions` — reordering a curriculum produces a new programme version and no content edit
  at all, which is the property that keeps customization from becoming the per-customer fork
  CONTENT_MODEL.md §1 forbids.
- **The draft is re-derived from the live tree**, exactly as 40.15's lesson draft is re-derived from
  the live rows. `POST /admin/program/versions/draft` walks skills → topics → lessons in tree order,
  skips archived lessons, and pins each to
  `ILessonVersionService.EnsurePublishedVersionIdAsync` — the same resolver an exercise submission
  goes through, so a programme and the progress recorded against it can never disagree about which
  snapshot a lesson currently is. It runs **before** the write scope opens, for the same reason
  `ExerciseService` does: losing the mint race raises a unique violation, and a unique violation
  aborts the whole transaction it happens in.
- **Publishing freezes the structure, in the database.** Two triggers, and the one on `ProgramItems`
  is the important half — that is where a retroactive reorder would actually be written, and it
  covers `DELETE` as well, because removing a lesson from a frozen programme is the same edit from
  the other side. A cascade from deleting the version itself is let through by the "parent row is
  already gone" branch.
- **Publishing an unchanged programme mints nothing.** The draft's item tuples are compared with the
  last published version's, in order; identical means the draft is discarded and the existing version
  comes back with `createdNewVersion: false`. This is the programme's stand-in for 40.15's content
  hash, and it matters more here: a version that changed nothing would still tell every enrolled
  learner a new programme is waiting and then show them an empty diff, which is how a switch notice
  stops being read.
- **The diff is computed honestly.** Four buckets — added, removed, re-pinned, moved — because they
  mean four different things to whoever decides. `movedLessons` (same lesson, same snapshot,
  different place) is the entire content of a "reorder the skills" edit. And `isBreaking` on a
  re-pinned lesson is not the target version's own flag: a programme can skip several lesson versions
  at once, so it asks whether **any** published version between the two pins declared itself
  breaking. Reading only the target would hide a changed correct answer behind a later typo fix,
  which is the 40.16 failure restated one level up.
- **Only the learner moves their own pin.** `POST /admin/program/enrollments` puts a learner with no
  pin on the newest published version and is idempotent — somebody who already has a pin comes back
  unchanged, never moved. `POST /program/switch` acts on the caller and names the target version, so
  a version published between showing the diff and accepting it cannot be the one they land on. There
  is deliberately no route by which an administrator moves somebody else, because "a manager on
  lesson 8 of 21 must not find the programme rearranged" is a claim about which code paths exist.
- **Enrollment does not gate access to lessons, and there is no "programme version 1".** An
  organization that has published nothing has no pins, and its people read the live tree exactly as
  they did before the phase. Minting a programme from whatever the seeder happened to load and
  pinning everybody to it would freeze them onto a curriculum nobody authored — see
  `docs/DECISIONS.md` (2026-08-17). The consequence, stated plainly: until the frontend reads
  `GET /program`, the pin changes nothing about what a learner sees on the existing screens.


### Copy-on-write overrides and the staleness queue (Phase 40.18)

The block that lets a customer edit the shared library without forking it
([CONTENT_MODEL.md](TENANCY/CONTENT_MODEL.md) §1, §2.6). No new table: 40.15 had already built the
override columns for lessons, and this brings `Techniques` and `ReferenceMaterials` — the two the
roadmap warns are easy to forget — to the same shape.

- **A copy is created only by `POST /admin/content/overrides/{kind}/{baseId}`**, and that route is
  reachable only from a person pressing "edit". `ContentOverrideService` is not wired to a consumer,
  a hosted service or an organization-created event. This is the block's whole point: an organization
  handed a private fork of the curriculum at onboarding stops receiving improvements to the base for
  ever, fifteen customers means fifteen forks, and every later base fix becomes fifteen merges by
  hand.
- **Read resolution is `ContentOverrideResolution`, called explicitly on the learner-facing paths.**
  The tenancy query filter admits "mine or global", so without it an organization that overrode three
  lessons sees them twice. Applied in `SkillTreeService` (the lesson-count denominator),
  `ExerciseService` (lesson lists, next-lesson unlocking, skill completion), `TechniqueService` and
  `ReferenceService`. Deliberately **not** applied on the authoring paths, whose job is showing the
  base next to the override, nor for platform-wide callers, where one customer's edit would hide a
  global lesson from Sellevate staff. For techniques it is correctness rather than tidiness: an
  override carries its base's slug on purpose, so an unresolved lookup by slug matched two rows.
- **Staleness is derived, never stored.** `GET /admin/content/overrides?staleOnly=true` compares each
  override's fork marker against the base as it stands right now — `LessonVersion` ids for lessons, a
  content fingerprint for the other two, which have no version table. Marking at publish time was
  rejected because it is refused by the database: it would mean writing rows into organizations the
  publisher is not in, and the RLS `WITH CHECK` clause is the one the 2026-08-16 role split
  deliberately did not widen for platform staff. A background sweep was rejected for lagging, and
  while it lags the queue claims an override is current when its base has already moved.
- **Nothing merges, and the API computes no diff.** The review endpoint returns three documents. A
  textual diff of prose is the first half of a merge, and the pressure to "apply the non-conflicting
  hunks" starts the moment one exists — after which a rule nobody chose is grading a real
  salesperson.
- **"Take the new base" archives the override; it never deletes it.** `UserExerciseAttempt`,
  `UserLessonProgress` and `UserTechniqueProgress` point at these rows without a foreign key (40.16's
  decision), so deleting one to tidy a review queue orphans exactly the history 40.15 and 40.16 exist
  to protect. `IsArchived` was therefore added to `Techniques` and `ReferenceMaterials` to match the
  column `Lessons` already had.
- **Four admin controllers were opened to organization administrators**, because otherwise an
  override is a copy nobody but Sellevate can edit and the third review action has no route at all.
  `AdminLessonsController`, `AdminExercisesController`, `AdminTechniquesController` and
  `AdminReferenceController` now carry `RequireOrgAdmin` plus `ContentAuthoringGuard`: a row with an
  owner is writable by that organization, a row without one needs platform rights, and creation from
  nothing stays platform-only.
- **`ContentAuthoringGuard` is in C# because RLS cannot do this job.** The content policy is
  `OrganizationId IS NULL OR = current` in `WITH CHECK` as well as `USING`, since a customer must be
  able to read the shared library; read as a write rule it says any organization may write a row with
  a null owner. Three CHECK constraints (`CK_*_OverrideHasOwner`) state the half the database *can*
  enforce: a row with a parent always has an owner.
- **`[TenantTransaction]` on those four controllers closes a gap `TenantTransactionScope` had been
  documenting about itself since 40.10.** They opened no transaction, so `SET LOCAL
  app.organization_id` never ran; while all content was global that cost nothing, and the moment an
  organization owned a row they would have stopped seeing it — fail-closed, invisible in the logs.
- **Not in this block:** the review screen (the frontend was not touched — 40.20), a version table
  for techniques and reference materials, and any parameterization of base content from an
  organization profile (40.19).
### Placeholder substitution on read (Phase 40.19)

`{{organization.product}}` and its five siblings resolve from the caller's organization profile at
**render time**. Full syntax and authoring rules:
[CONTENT_PARAMETERIZATION.md](CONTENT_PARAMETERIZATION.md). Three things belong here rather than
there, because they are properties of this service.

- **Nothing on the write side renders.** `LessonSnapshotSerializer`, `ContentSnapshotSerializer`, the
  publish path and every `/admin/*` authoring read see the template exactly as stored. That is what
  keeps `LessonVersion.ContentHash` identical across organizations for the same base lesson — render
  before hashing and every customer gets their own snapshot row, which is 40.18's fork reached by
  accident and without any of its guard rails.
- **The grader renders too, and that is not symmetry for its own sake.** `SubmitExerciseAnswerAsync`
  renders the exercise content before handing it to the evaluation strategy, because a question
  rendered for the learner and unrendered for the grader marks correct answers wrong: the
  deterministic strategies compare option text, and the AI strategy would be judging an answer to a
  question it was not shown.
- **`banned_claims` is appended to the AI grading prompt**, in the same words ai-service appends to
  the persona prompt (`OrganizationProfilePromptBuilder`, BuildingBlocks). Enforcing it only on the
  persona side would be worse than nothing — a persona that stays silent while the grader keeps
  rewarding the forbidden claim teaches the rep to say it anyway.

The profile is read through `IOrganizationProfileProvider`, scoped and memoized per request: one
lesson open resolves placeholders in a title, in every exercise and possibly in a grading prompt,
from a row that cannot change mid-request. Platform-wide callers get the empty profile — in platform
mode the query filter admits every organization at once, so picking a row would show Sellevate staff
a lesson with some customer's product name in it.

### Assignments (Phase 40.21, thresholds 40.22, issuing 40.23, repeats 40.24, dashboard 40.25, the РОП's push 40.26)

`Assignments` / `AssignmentProgressRecords` ([ASSIGNMENTS.md](TENANCY/ASSIGNMENTS.md) §1). Stage E
opens here: the РОП turns an internal training session into short, dated, targeted practice for named
people and can see who actually did it.

Strict tenant data, plain-equality RLS, same shape as the programme tables and for the same reason —
there is no such thing as a global assignment.

- **A separate entity, not a repurposed programme, and the difference is not cosmetic.** The skill
  tree is long, sequential and self-paced; an assignment is days long, aimed at named people and
  worthless once its deadline passes. Sharing a table would give the curriculum a deadline it has no
  meaning for and the assignment a version-and-pin lifecycle nobody wants for a five-day task.
- **An assignment is references, and nothing else** — the same property `ProgramVersionService` has.
  Not one write in `AssignmentService` touches `Lessons`, `LessonVersions`, `Exercises` or
  `ReferenceMaterials`. Its exercise set is a pinned `LessonVersion`, so the bodies stay in
  `Exercises.SerializedContent`, the eleven existing renderers play it with no new code, and there is
  no second grading path. Pointing at mutable `Exercise` ids instead would repeat exactly the defect
  40.16 removed from progress.
- **`completionRule` is required and has no default.** A default would have to mean "no threshold",
  and an assignment that completes on a click is the compliance-theatre failure ASSIGNMENTS.md §1.1
  exists to prevent — so the column has no default, the API cannot omit it, and a check constraint
  refuses anything that is not an object naming its `kind`. The *vocabulary* is 40.22's and is
  described below. `repeatSchedule` still gets only the shape check (40.24).
- **The audience column holds the rule, not the people.** The employee list lives in identity-service;
  this database has only the platform-global `UserReplicas`, so learning-service cannot resolve
  "the whole team" into names and a resolved list here would be a stale copy of somebody else's data.
  40.23 resolves it at issue time, and the `AssignmentProgressRecords` rows it writes are the
  authoritative record of who was actually asked.
- **Issue freezes what was asked, in the database.** `Assignments_reject_frozen_change` refuses
  changes to `SourceType`, `SourceRef`, `Content`, `CompletionRule` and `ActivatedAt` once the row
  leaves `draft`, and freezes a closed row whole. Title, goal, audience, deadline and repeat schedule
  stay writable deliberately — adding three people to a running assignment and extending a deadline
  are ordinary acts, and a trigger that forbade them is one 40.23 and 40.24 would have to break. The
  service refuses the same edits first, with a message naming the fields, because an administrator who
  believes they moved a threshold and did not is worse off than one who is told they cannot.
- **`AssignmentProgressRecords` gained its row *creator* in 40.23** (see below), and the shape of it
  was settled here: a row exists because somebody was **asked**, never because somebody happened to
  do the work. The rejected alternative — creating a row lazily on first activity — would make "who
  has not started", the single most actionable question in ASSIGNMENTS.md §5, an inference from
  absent rows instead of a query over present ones, and would put anybody who practised a referenced
  lesson for their own reasons on the РОП's screen as though they had been assigned it. 40.22 wrote
  the *updater*, below; 40.23 writes the rows.
- **Learner-facing routes arrived in 40.23**: `GET /assignments/active`, together with the audience
  resolution it depends on.

### Completion is a quality threshold (Phase 40.22)

The block's whole claim: if completion means "opened everything", a team clicks through in four
minutes, the dashboard reads 100%, and the number is a lie the РОП eventually catches. Two rule kinds,
both from the roadmap, parsed by `AssignmentCompletionRuleReader` — strictly on write, tolerantly on
read, the same asymmetry `AssignmentDocumentSerializer` uses for the other jsonb columns.

| `kind` | One attempt is | Met when |
|---|---|---|
| `dialog_score` (`minimumScore`, `requiredCount`) | one graded practice conversation on one of the assignment's `dialog_scenario` items | that many conversations have each cleared the bar |
| `exercise_accuracy` (`minimumAccuracyPercent`) | one exercise submission against the pinned `lesson_version` | every exercise in the set has been attempted **and** correct ÷ all submissions clears the bar |

- **It does not score anything; the roadmap says the evaluation reuses existing scoring and it does.**
  Exercise correctness comes from the `UserExerciseAttempt` rows the ordinary submit path writes, and
  accuracy is `correct ÷ all` — the same definition `LessonAccuracyService` already reports to the
  admin panel. A conversation's grade comes from ai-service's own feedback score, which 40.22 added to
  the `dialog.evaluated` contract as `qualityScore` (normalized 0–100) because the pre-existing
  `rawScore` field carries XP, not a grade.
- **`AssignmentThresholdConsumer` is the writer**, listening to `dialog.evaluated` and
  `exercise.completed`. A consumer rather than an inline call at the end of
  `SubmitExerciseAnswerAsync`, because half the evidence arrives from another service and two writers
  of the same two columns would mean two failure modes and two idempotency stories
  ([BACKGROUND_JOBS.md](TENANCY/BACKGROUND_JOBS.md) §4d).
- **Everything is recomputed, never incremented, and that is the whole idempotency story.**
  `AttemptCount` and `BestScore` are derived from the attempt rows on every evaluation, so a
  redelivered message leaves the same values behind. A graded conversation is stored once in
  `UserDialogScores` under a unique index on `(organization, user, session)`; `exercise.completed`
  writes nothing at all and is used purely as a trigger.
- **Two states kept apart on purpose.** `in_progress` means the work is unfinished; `failed_threshold`
  means it is finished and under the bar. Collapsing them hides the person who needs coaching among
  the people who have not started, and the roadmap calls that row the most valuable one on the screen.
  `completed` is terminal — a threshold cleared once stays cleared, and a later weaker attempt is
  practice rather than a demotion.
- **Two details that make the bar unfakeable.** Accuracy counts *submissions*, so brute-forcing a set
  until everything is green lowers it instead of raising it; and the accuracy score is withheld until
  every exercise in the set has been attempted, because one lucky answer out of twenty is otherwise
  100%. Work recorded before the assignment was issued never counts: the window opens at the later of
  `ActivatedAt` and `OpensAt`.
- **Attempts are matched by exercise id, not by lesson version id**, even though 40.16 binds every
  attempt to a version. The pinned snapshot decides *which* exercises the threshold covers; the
  learner's submit path binds their attempt to whatever version is published the day they answer, so
  filtering on the pinned id would make an assignment silently unreachable the moment its lesson is
  republished mid-flight.
- **It updates rows, it never creates one**, and an unreadable or unmeasurable rule leaves the row
  alone with a warning — fail-closed, because being short of a threshold is recoverable and a
  completion nobody measured is not.

### Issuing an assignment to people (Phase 40.23)

The row **creator** 40.21 and 40.22 both deliberately left out. `POST /admin/assignments/:id/activate`
now does three things in one transaction: turn the audience rule into named people, write one
`not_started` progress row per recipient, and stage one `assignment.issued` outbox event per
recipient. The outbox is what makes "was asked" and "was told" atomic.

- **The roster comes from identity-service over HTTP**, `GET /internal/memberships/active`, resolved
  *before* the write transaction opens so a network round trip never sits inside a transaction
  holding locks on the progress table. learning-db cannot answer the question itself: `UserReplicas`
  is platform-global and says nothing about who belongs where. A Kafka membership replica was
  rejected on its failure mode — a replica that lags or was never backfilled issues the assignment to
  nine people out of forty and reports success ([DECISIONS.md](DECISIONS.md), 2026-08-18).
- **Every audience kind is filtered through the live roster**, including an explicit `userIds` list.
  Leavers are dropped with a log line; an audience resolving to nobody is a `400`; `group` is a `400`
  because no group exists in the platform yet. When identity cannot be reached the whole issue is
  refused with a **`503`** and nothing is written.
- **Editing an active assignment's audience re-resolves and tops up**, adding rows and notices and
  never removing anybody. That is how somebody hired after the issue joins work already running.
- **Bounded at 2000 recipients per issue** — a ceiling rather than paging, because the number it
  guards against is a mistake rather than a big customer.

`GET /assignments/active` is the learner side: their own unfinished assignments, soonest deadline
first, driven off *their progress rows* so an assignment nobody issued to them cannot appear. It
carries `completionRule` verbatim so the screen names the bar rather than a status word, and it
**writes nothing** — a "mark as opened" route would be a second writer of columns 40.22 owns, with a
different idea of what "started" means.

`GET /internal/assignments/practice-context` is the ai-service side: when a dialog session starts on a
mode some open assignment names, ai-service fetches that assignment's framing and persona and injects
them through `AssignmentPracticePromptBuilder`. The persona is deliberately **not** in
`ActiveAssignmentDto` and **not** accepted in a request body — the browser starting the session
belongs to the person being graded against it.

`POST /admin/assignments/:id/remind` publishes `assignment.reminder` to everybody not `completed`,
`failed_threshold` included: that is the row the РОП most needs to reach.

### Repeating an assignment automatically (Phase 40.24)

The roadmap's whole reason for the feature: an internal training's effect decays in two to three
weeks, so a one-shot assignment reproduces exactly the failure it is meant to fix.
`AssignmentRepeatSweepService` re-issues a **shortened version** at the offsets `repeat_schedule`
names — `{"kind":"fixed_offsets","offsetDays":[7,21]}`, the list optional and defaulting to those two.

- **A wave is a new `Assignments` row**, created already `active`, linked to its origin by
  `RepeatOfAssignmentId` and a 1-based `RepeatWaveIndex`. A second round inside the same row was
  rejected because `AssignmentProgressRecords` carries one `BestScore` per person: the second wave's
  result would overwrite the first's, destroying the only evidence that the training decayed
  ([DECISIONS.md](DECISIONS.md), 2026-08-18). A repeat never carries a schedule of its own — the
  database refuses it — so a series is one level deep and cannot cascade.
- **Idempotency is the row's existence**, guarded by a unique partial index on
  `(RepeatOfAssignmentId, RepeatWaveIndex)`. Nothing is incremented and nothing is stamped, which is
  40.22's rule again — and here it is also the only option available: a sent-ness column on the origin
  would be unwritable the moment the origin is `closed`, because the 40.21 freeze trigger refuses any
  update to a closed row.
- **It reuses the 40.23 fan-out verbatim.** `AssignmentFanOut` was extracted from `AssignmentService`
  so a human pressing "issue" and the sweep write the same pair of facts — one progress row and one
  `assignment.issued` outbox event per recipient, in one transaction — rather than two copies with two
  idempotency stories. No new event family: notification-service dedupes on the assignment id, and a
  repeat *is* a new assignment id.
- **The cohort is the origin's recipients ∩ the live roster**, whatever became of them. Re-resolving
  the audience rule would hand a shortened refresher to everybody hired since and change the
  denominator between waves; filtering by outcome would mean the product silently stops asking the
  `failed_threshold` person 40.22 calls the most valuable row on the screen.
- **Shortened means less, not easier**: theory (`reference_material`) is dropped unless it is all the
  assignment has, and `dialog_score.requiredCount` is halved rounded up. Score bars are copied
  untouched.
- **A closed origin still repeats**, and a wave more than `RepeatCatchUpDays` (default 3) late is
  dropped rather than delivered. Both are deliberate and both are in
  [DONT_FORGET.md](DONT_FORGET.md); the cancel path is editing `repeat_schedule` while the assignment
  is still active.

### The РОП's dashboard and the two-way feedback loop (Phase 40.25)

40.21–40.24 built the machinery and nobody could look at any of it. This block is the read side, plus
the one place the loop runs back the other way. Full argument in [DECISIONS.md](DECISIONS.md)
(2026-08-18); ASSIGNMENTS.md §4 is the design.

- **`GET /admin/assignments/{id}/dashboard`** (`AssignmentDashboardService`) — the funnel, the named
  people behind it and every wave of the repeat series with its own funnel, in one response.
  Read-only, nothing is stored: a denormalized funnel column would be a second writer of a fact that
  already has one, and 40.22's derive-never-increment rule is what makes the numbers survive a Kafka
  redelivery. `failed_threshold` is a **fifth** funnel stage rather than a subset of "completed", for
  the reason §1.1 gives.
- **The leaver problem 40.23 recorded is closed here.** The dashboard asks
  `IOrganizationMemberDirectory` who still holds an active membership and marks each row, then reports
  `leftOrganizationCount` and `assignedActiveCount` alongside the raw counts. The progress row still
  stays — it is the record that somebody was asked. **This roster read is fail-open**, unlike the one
  at issue time: `null` says "we could not check", which is a different statement from zero, and a
  read that refuses to draw the screen to withhold one annotation is the worse trade.
- **`GET /admin/team/skill-map?days=`** (`TeamSkillMapService`, `Features/TeamInsights/`) — one matrix
  read along both axes the roadmap asks for: per team, accuracy per skill; per manager, the
  sales-funnel stage they sag on. The stage vocabulary is the platform's existing `Skill.Stage` /
  `SkillStages` pair, not a second one. Skill attribution goes `UserExerciseAttempts → Exercises →
  Lessons → Topics → Skills`; attempts whose exercise no longer exists are reported as
  `unattributedAttemptCount` rather than folded in, the same call
  [ANALYTICS_SERVICE.md](ANALYTICS_SERVICE.md) makes for `unversionedAttempts`. A cell below five
  attempts reports **no** percentage: two right answers out of two is 100% and is a fact about
  nobody.
- **`DialogReviewNotes`** (`Features/DialogReviews/`) — one table for both directions of §4.1, under a
  `Kind` column: `coaching_note` (РОП → manager, closed by being read) and `score_dispute` (manager →
  РОП, closed by a verdict). Routes: `/admin/dialog-reviews` (queue, create a note, resolve a
  dispute) and `/dialog-reviews` (the manager's inbox, file a dispute, acknowledge a note).
- **Every write into that table starts from a `UserDialogScores` row and never from the request
  body.** The session id, the manager, the scenario and the frozen grade are read out of learning-db,
  so "the РОП cannot address a note at somebody else's employee" is a property of an RLS-protected
  query rather than a validation somebody has to remember. It also means an ungraded conversation
  cannot be annotated at all — the right refusal, since it has no grade to dispute.
- **An upheld dispute records `AdjustedScore` and does not apply it.** A hand-edited score would be
  overwritten by the next redelivery (40.22 recomputes everything), and a threshold negotiable by the
  person being measured is the four-minute completion 40.22 exists to prevent, reached another way.
  Retro-scoring is a product decision in [DONT_FORGET.md](DONT_FORGET.md).
- **Nothing here reads Mongo.** The quotes come from ai-service's own
  `GET /admin/dialog-sessions[/{id}]`, so `IDialogSessionRepository` stays the single holder of the
  session collection (TENANCY.md §1.6). The screen asks each service for what it owns.

### Closing the loop from metric to content (Phase 40.31)

The heat map above stops being a report. Four routes under `/admin/team/skill-gaps`
(`TeamSkillGapService`, `Features/TeamInsights/`), one table, one column.

- **`GET /admin/team/skill-gaps?days=`** — what to do next. It calls `ITeamSkillMapService` and
  derives from the matrix, rather than re-aggregating: a red cell with no suggestion, or a suggestion
  for a cell that is not red, would both be bugs the screen could not explain.
- **A gap is three conditions**: at least 20 attempts on the stage inside the window, team accuracy
  at or below 60%, and at least two managers with a reportable cell at or below it. The third is what
  makes it «провал команды» rather than one person's bad week — for one person the matrix already
  names them (`weakestStageKey`) and the answer is a conversation, not fifteen exercises. All three
  are the agent's product decisions with their reasoning in [DECISIONS.md](DECISIONS.md).
- **`POST /admin/team/skill-gaps/{stageKey}/content` is the one button.** It starts an ordinary 40.27
  run: same checkpoint, same sufficiency threshold, same archived arrival. The only differences are
  that the run's material is **composed** — from the measurement and the organization profile replica,
  deterministically, no model involved — and that it carries `GapSourceRef`. An organization with an
  empty profile gets a run in `insufficient` with 40.28's own codes, which is the correct answer and
  not a defect: we do not know enough about that company to write exercises for them.
- **Suggestions are computed; only refusals are stored.** `TeamSkillGapDismissals` is the block's only
  table and holds one live row per stage. Everything else — which stages qualify, which are being
  worked on, which were addressed recently — is derived on every read, so a gap that closes stops
  being offered without any writer noticing. The shape 40.18 used for staleness and 40.25 for the
  funnel.
- **Anti-spam is the reason two of the four routes exist.** A dismissal lasts 90 days (the heat map's
  own default window) and is broken early if the number falls ten points further; a live run for a
  stage suppresses it outright and a second press returns *that run* rather than buying a second
  lesson; a completed run keeps its stage quiet for 30 days. Every suppressed gap is still returned,
  with its reason and expiry — a panel that shows nothing cannot be told apart from a broken one.
- **`POST /admin/assignments` gained `contentGenerationJobId`**, and it **derives** `SourceType` and
  `SourceRef` from the run instead of believing the body: `gap_detected` +
  `skill-gap:<stage>@<date>` for a dashboard-started run, `training` + `lesson-version:<uuid>` for a
  pasted one. That is the loop closed, and it also gave `training` its first writer — 40.21 defined
  the value and nothing had ever set it.

### Non-completion as a working scenario (Phase 40.26)

The block that makes the РОП act instead of read. No new table, no new column, no migration, no new
job. Full argument in [DECISIONS.md](DECISIONS.md) (2026-08-18); ASSIGNMENTS.md §5.1 is the design.

- **`IOrganizationMemberDirectory` became one call returning two facts.**
  `GetRosterAsync` → `OrganizationRoster { MemberIds, AdministratorIds }`, from the same
  `GET /internal/memberships/active`, which identity-service widened with an `administratorUserIds`
  subset. That capability is what 40.25's dispute push was waiting on. `AdministratorIds` is
  **nullable and null means "this identity-service predates 40.26"**, which is deliberately not the
  same value as an empty list: collapsing the two would let a rolling deploy swallow a digest and
  leave nothing behind to notice.
- **`AssignmentDeadlineNoticeService` publishes both families in one transaction.** The manager
  notices it already sent, plus one `assignment.deadline.digest` per administrator naming up to five
  people who have never opened the assignment, with the true total beside them. **No digest when that
  list is empty** — «все молодцы» is what teaches a РОП to skip the channel — and no notices at all
  for an organization whose administrators cannot be addressed, because the stamp is permanent.
- **`POST /admin/assignments/{id}/remind` gained `?scope=` and a roster read.** `not_started` is the
  set the digest names and is what its action link asks for; `unfinished` is the default and 40.23's
  behaviour. The roster read is **fail-closed** (503, like `activate`): this was the last path in the
  feature that could still mail an ex-employee their former employer's homework.
- **`DialogReviewService` now pushes a filed dispute** to every administrator except its author, with
  the manager's name, the grade they contest and their own sentence. That read is **fail-open**: the
  row is already written and already in the queue, so identity-service being unreachable costs the
  notice and never the dispute.
- **One behaviour changed outside this block's own code**: `assignment.reminder`'s dedupe key
  coarsened from the exact instant to the hour, in notification-service. The digest puts the remind
  button in front of every administrator at once, and five presses in one meeting used to be five
  separate messages to the same manager.

### Background jobs

| Job | Mode | Why it is safe |
|---|---|---|
| `OutboxRelayBackgroundService` | system | Reads `OutboxMessages` only, which has no `OrganizationId` filter and no RLS policy — the tenant travels in the envelope payload (TENANCY.md §1.7). The single legitimate cross-tenant reader. |
| `UserReplicaConsumer` | platform-global (`RequiresOrganization => false`) | Projects identity's cross-org user table; `UserReplicas` has no organization column. |
| `OrganizationProfileConsumer` (40.19) | tenant from the envelope (`RequiresOrganization` inherited `true`) | Projects `organization.profile.updated` into `OrganizationProfileReplicas`, which is strict tenant data — so the write happens under ordinary tenant context, with no RLS widening. The first consumer here that does *not* opt out. |
| `LessonVersionBackfill` (startup, once) | system | Mints "version 1" for never-published lessons; sees the global library only. |
| `AssignmentDeadlineSweepService` (40.23, digest 40.26) | **per-organization iteration** over a **system** enumeration | Warns everybody who has not finished an assignment whose deadline is inside the lead window (24h by default), sends the organization's administrators a digest naming who has not **started** it (40.26, and only when that list is non-empty), then stamps `Assignments.DeadlineNoticeSentAt` — one timestamp for both, because they announce the same date. The enumeration reads one column — organization ids of rows already known to be due — and everything after it runs in a fresh scope with a concrete organization set. It consults the live roster before warning anybody, so a progress row belonging to somebody who has left does not mail them their old employer's deadline. **Needs `BYPASSRLS` for the enumeration only** — see [BACKGROUND_JOBS.md](TENANCY/BACKGROUND_JOBS.md) §4e and DONT_FORGET. |

| `AssignmentRepeatSweepService` (40.24) | **per-organization iteration** over a **system** enumeration | Issues the repeat waves whose day has come, as new assignments linked to their origin. The enumeration reads one column — organization ids of assignments that carry a repeat schedule and were issued recently enough for a wave to still be pending — and everything after it runs in a fresh scope with a concrete organization set. It consults the live roster before issuing anything, and a failure to read it skips that organization for the tick with nothing recorded, so the next tick retries. **Needs `BYPASSRLS` for the enumeration only** — and this is the job where that matters most, because its output is invisible by nature: nobody notices a repeat that was never created. See [BACKGROUND_JOBS.md](TENANCY/BACKGROUND_JOBS.md) §4f and DONT_FORGET. |
| `ContentGenerationSweepService` (40.27) | **per-organization iteration** over a **system** enumeration | Advances the content pipeline one step per run: the structuring call, then — only after a human approved the structure — the generation call that writes a lesson. **Needs `BYPASSRLS` for the enumeration only**; unlike the two above, its silence is loud, because a person is watching a spinner. See [BACKGROUND_JOBS.md](TENANCY/BACKGROUND_JOBS.md) §4h. |
| `ContentAdaptationSweepService` (40.32) | **per-organization iteration** over a **system** enumeration | Answers a few items of a batch tone rewrite or content review per tick — one LLM call per exercise — and writes a **proposal** onto the item. It cannot write an `Exercise`: applying a proposal is an admin request, which is where «никогда не автоприменение» actually lives. The batch carries the lease, the item carries the idempotency, so an interruption costs the one call in flight. **Needs `BYPASSRLS` for the enumeration only** — see [BACKGROUND_JOBS.md](TENANCY/BACKGROUND_JOBS.md) §4i and DONT_FORGET. |

Until 40.23 there was no per-organization iteration job in learning-service; the deadline sweep was
the first and 40.24's repeat sweep is the second. An **unset tenant is an exception, never a licence**: `KafkaConsumerBackgroundService` throws when a consumer that requires an
organization gets an envelope without one, the query filters resolve to "no rows" rather than "all
rows", and `TenantSaveChangesInterceptor` throws on any tenant-scoped write with no context.

### Operational steps that are NOT in the migration

Migration `20260815152225_AddOrganizationId` adds the columns and turns RLS on. It deliberately
contains no `CREATE INDEX` and no backfill:

- `docs/TENANCY/sql/40.10_learning_organization_backfill.sql` — replaces the all-zeros placeholder
  organization on the progress tables. Until it runs, RLS hides every pre-existing progress row.
- `docs/TENANCY/sql/40.10_learning_organization_indexes_concurrently.sql` — `CREATE INDEX
  CONCURRENTLY`, `pg_index.indisvalid` check, then drops the superseded indexes.
- `scripts/tenancy-learning-organization-rollout.sh` drives both; default mode writes nothing.

Neither has been run against any real database — see `docs/DONT_FORGET.md`.

Phase 40.15's migration (`20260817193243_AddLessonVersioning`) is the opposite case and deliberately
so: it creates its own indexes and does its own slug backfill, and there is **no** companion
`_indexes_concurrently.sql` or backfill script. `LessonVersions` is created empty by the migration,
`Lessons` is a content table of a few hundred rows, and the slug value is derived from each row's own
primary key — so there is no long lock, no separate maintenance window, and no interval in which data
is invisible. Slug uniqueness is correctness, and deferring a correctness constraint to a script
somebody has to remember is the worse trade (the same call 40.13 made for the four small gamification
tables). What does exist is `docs/TENANCY/sql/40.15_lesson_versioning_verify.sql` — read-only, safe
with the service up, and not run against anything either.

Phase 40.16's migration (`20260817195247_AddProgressLessonVersionBinding`) sits between the two. It
adds its two columns itself — both nullable, so Postgres treats it as a catalogue-only change with no
rewrite and no long lock even on the two tables that grow with usage — but leaves both of its
operational steps outside:

- `docs/TENANCY/sql/40.16_progress_version_backfill.sql` — binds existing attempts and lesson
  progress to their lesson's earliest published version, in batches, refusing to run without
  `BYPASSRLS`. Requires the service to have started once first, so `LessonVersionBackfill` has minted
  the versions it binds to.
- `docs/TENANCY/sql/40.16_progress_version_indexes_concurrently.sql` — the two read-path indexes,
  under the exact names EF generates (including EF's `~` truncation marker; renaming them makes the
  next `dotnet ef migrations add` emit a table-locking `CreateIndex`).

Unlike 40.10–40.13 there is **no window in which data is invisible**: nothing filters on
`LessonVersionId`, so the deployment and the backfill do not have to share a maintenance window.
Neither file has been run against any database.

Phase 40.17's migration (`20260817203021_AddProgramVersioning`) is the cleanest of the four and has
**no operational step at all**. All three tables are created empty by the migration, so its indexes
are built over zero rows and there is no `_indexes_concurrently.sql`; nothing is backfilled, because
there is nothing to backfill — no programme exists until an administrator builds one. What does exist
is `docs/TENANCY/sql/40.17_program_versioning_verify.sql`, read-only, safe with the service up, and
not run against anything either.

## Coupling broken during extraction

| Monolith coupling | Resolution in learning-service |
|---|---|
| `ExerciseService` writing `UserXp` / `UserStreak` rows + calling `IGamificationService` for base XP | Removed. On a correct submission Learning emits `exercise.completed`; Gamification grants XP / updates streaks. `XpEarned` in the submission DTO is `0` (shape unchanged). |
| `ExerciseService` calling `IAchievementService.EvaluateAchievementsAfterSubmitAsync` | Removed. Achievements are unlocked by Gamification reacting to the events. `NewlyUnlockedAchievementKeys` is empty (shape unchanged). |
| `ExerciseService` writing `StreakMilestone` notifications | Removed. Gamification produces `streak.milestone`; Notifications writes the inbox entry. |
| AI grading strategies calling OpenAI directly + reading `ExerciseTypePrompt` | The deterministic strategies stay local. The 5 AI types call ai-service `POST /ai/evaluate`, passing the global `ExerciseTypePrompt` text (Learning still owns the prompts) plus the raw exercise content + user answer; ai-service runs the LLM grading and returns the verdict. |
| `SkillTreeService` reading XP/streak/goals for the `/skill-tree` aggregate | Those fields are owned by Gamification (Phase 7). Learning serves the skill-progress fields truthfully (computed from its own lesson progress) and returns `currentStreakDayCount`/`totalXp`/`weeklyXp`/`dailyXp`/goals as `0` — DTO shape unchanged, composed for real once the frontend reads gamification aggregates. |

## Lesson progression / unlocking

Lessons unlock sequentially. On the first correct/attempted submission that transitions a
lesson to `completed`, `ExerciseService.UnlockNextLessonInTopicAsync` marks the **next**
lesson `Available` (creating its `UserLessonProgress` row if missing).

"Next" is resolved across the whole skill, not just the current topic
(`ResolveNextLessonInSkillAsync`): the next lesson in the same topic by `OrderInTopic` wins
first; when a topic's last lesson is completed it **rolls over** to the first lesson
(`OrderInTopic`) of the next topic by `Topic.OrderInSkill`. This is what lets the next topic
open once the current one is finished. Regression covered by
`ExerciseServiceEventEmissionTests.CompletingLastLessonInTopic_UnlocksFirstLessonOfNextTopic`.

## Events produced

| Topic | When | Payload (camelCase on the wire) |
|---|---|---|
| `exercise.completed` | every exercise submission | `{ userId, exerciseType, score, isCorrect }` |
| `lesson.completed` | a lesson transitions to completed | `{ userId, lessonId, bestScore }` |
| `skill.completed` | the last lesson of a skill completes | `{ userId, skillId }` |
| `technique.mastery.changed` | a user's technique mastery/level changes | `{ userId, techniqueId, level, masteryPercent }` |
| `assignment.issued` (40.23) | one per recipient, when an assignment is issued or its running audience widens | `{ assignmentId, userId, title, goal, deadline }` |
| `assignment.deadline.approaching` (40.23) | the deadline sweep, once per assignment per unfinished recipient | `{ assignmentId, userId, title, deadline }` |
| `assignment.reminder` (40.23) | a РОП presses "remind" | `{ assignmentId, userId, title, deadline, requestedAt }` |
| `assignment.progress.changed` (40.25) | a progress row moves between the four funnel states | `{ assignmentId, userId, previousStatus, status, bestScore, attemptCount }` |
| `dialog.review.commented` (40.25) | the РОП comments on a fragment of somebody's graded call | `{ noteId, userId, sessionId, quotedText, comment }` |
| `dialog.review.resolved` (40.25) | the РОП rules on a disputed AI score | `{ noteId, userId, sessionId, outcome, disputedScore, adjustedScore, resolution }` |
| `assignment.deadline.digest` (40.26) | the deadline sweep, once per assignment per **administrator**, only when somebody has not started | `{ assignmentId, administratorUserId, title, deadline, notStartedCount, notStartedNames }` |
| `dialog.review.disputed` (40.26) | a manager files a score dispute, once per **administrator** except its author | `{ noteId, administratorUserId, subjectUserId, subjectDisplayName, sessionId, disputedScore, comment }` |

The first three match the gamification-service consumer contract verbatim
(`ExerciseCompletedEvent`, `LessonCompletedEvent`, `SkillCompletedEvent`). Consumed by
Gamification (XP/streaks/achievements/league) and Analytics (`exercise.completed`).

`assignment.progress.changed` (40.25) is consumed by **analytics-service only**, and only for its
`status` field, which becomes a bounded Prometheus label. It is published from
`AssignmentThresholdEvaluator` inside the transaction that writes the row, and only on an actual
state change — an attempt count ticking from three to four is not a funnel movement. The
per-organization funnel is **not** built from this topic and must not be; it is counted from the rows
by `AssignmentDashboardService`. See [ANALYTICS_SERVICE.md](ANALYTICS_SERVICE.md).

The two `dialog.review.*` topics (40.25) are consumed by notification-service only. Both are
addressed to the manager, including a rejected dispute: a complaint closed in silence recreates the
black box the mechanism exists to open. The quoted fragment travels with the event rather than being
fetched, because notification-service has no database beyond its inbox and a notice reading "you have
a comment" is one more thing to ignore.

The three `assignment.*` topics are consumed by notification-service only. Every one of them is
**per recipient rather than per assignment**: the partition key is the user id everywhere in this
system, and notification-service's dedupe is per recipient, so a batch event would have to be
unpacked into the same per-person keys inside the consumer — where a partial failure loses part of a
batch instead of retrying one message.

## Events consumed

`user.registered` / `user.updated` / `user.avatar.changed` / `user.deleted` →
keep the local `UserReplica` in sync (idempotent, dedupe on `eventId`).

`dialog.evaluated` + `exercise.completed` (Phase 40.22, `AssignmentThresholdConsumer`) → re-judge that
person's open assignments against their completion rules, and mirror a graded conversation into
`UserDialogScores`. This is the one place the service consumes a topic it also produces
(`exercise.completed`): the event is used purely as a "this person did something" trigger, and the
handler publishes nothing.

`organization.profile.updated` (Phase 40.19) → keep `OrganizationProfileReplicas` in sync, so
`{{organization.*}}` placeholders resolve without a call into organization-service on the read path
of every lesson. Full payload every time, so a dropped message is repaired by the customer's next
save rather than made permanent.

## Synchronous dependencies

`Learning → AI`: `POST /ai/evaluate` for the 5 AI-graded exercise types
(`spot_mistake`, `rewrite`, `ai_dialogue`, `evaluate_call`, `free_text`). The learner
is waiting for the grade in real time, so this is REST, not an event. Configured via
the `AiService:BaseUrl` option (`http://ai:8080` in compose).

`Learning → Identity` (Phase 40.23): `GET /internal/memberships/active`, from the assignment fan-out
and from the deadline sweep. Configured via `IdentityService:BaseUrl` (`http://identity:8080` in
compose), authenticated with `InternalAuth:ServiceSecret`, organization in the `X-Organization-Id`
header. **It raises rather than degrading**: an issue that cannot read the roster is refused with a
503, because an empty list and "we could not find out" must never be the same value — one of them
issues an assignment to nobody and reports success.

`AI → Learning` (Phase 40.23, the reverse direction): `GET /internal/assignments/practice-context`,
when a dialog session starts. Unlike the two above it **degrades to "no assignment"** on any failure:
practising is the product and an assignment's persona is an improvement to it, so learning-service
being down must not stop a practice screen from opening.

`AI → Learning` (C-3 audit fix, docs/AUDIT_CONTRACTS.md): `GET /internal/skills/lookup` returns
`{id, iconicName, title}` for every skill, so ai-service can label a `DialogBundle` with the skill
it belongs to. No `[TenantScoped]`, no organization header: skills are global content today
(`Skill.OrganizationId` is always null), and the call is made both by the platform-staff dialog
admin screen (no organization at all) and the learner-facing bundle list. Degrades to an empty map
on any failure, same as the practice-context client above.

## Routes flipped at the gateway

`/skills/*`, `/skills`, `/skill-tree`, `/program`, `/program/*` (40.17), `/lessons/*`,
`/lessons`, `/topics/*`, `/exercises/*`, `/reference/*`, `/reference`, `/techniques/*`,
`/techniques`, `/daily-quote`, and the learning `/admin/*` content routes (`/admin/skills`,
`/admin/skill-stages`, `/admin/topics`, `/admin/lessons`, `/admin/exercises`,
`/admin/exercise-type-prompts`, `/admin/reference`, `/admin/techniques`,
`/admin/daily-quotes`, `/admin/seeder`, `/admin/program` and `/admin/program/*` — 40.17).
`/profile/*` is intentionally NOT captured (owned by identity/gamification).

**Phase 40.25 added `/assignments/*`, `/admin/assignments`, `/admin/assignments/*`, `/admin/team/*`,
`/dialog-reviews`, `/dialog-reviews/*`, `/admin/dialog-reviews` and `/admin/dialog-reviews/*`.** The
first three of those are a fix rather than a new feature: 40.21–40.23 built the assignment routes and
never added them to the gateway, so the manager's assignment strip — shipped and mounted on the home
screen in 40.23 — could not reach this service through the gateway at all. Nothing caught it, because
the frontend checks (`tsc`, `vitest`) do not know the gateway exists and no test asserts that a
controller route has a gateway route. Recorded in [DONT_FORGET.md](DONT_FORGET.md).

**Phase 40.32 added one gateway entry, `/admin/content/{**catch-all}`, and finding that it was
missing is the trap paying for itself.** The block's own routes live under
`/admin/content/adaptations`, and the parse of `appsettings.json` turned up something worse than a
missing route for new code: **there has never been a route matching `/admin/content/*` at all**, so
40.18's `/admin/content/overrides` — the copy-on-write and staleness-queue API — has been unreachable
from outside the cluster since it shipped. One catch-all, no `Methods` restriction, covers both.
Recorded in [DONT_FORGET.md](DONT_FORGET.md), because it also means 40.18 was never exercised end to
end.

**Phase 40.31 added no gateway entry, and that was checked rather than assumed.** Its four routes live
under `/admin/team/skill-gaps`, which the `learning-admin-team` route
(`/admin/team/{**catch-all}`, no method restriction) already covers — including the `DELETE`. The trap
40.25 documented above is the reason this sentence exists at all: a new route under a path with no
catch-all is a silent 404 that no test in this repository can see.

After this flip the only public route still served by the monolith catch-all is
`/admin/users/*` (admin user management: list/detail, moderation rename, avatar
reset, role change). It was never part of any service's scope — Identity owns the
user aggregate but never took these admin routes. Phase 9 must move `/admin/users/*`
(naturally to identity-service) before the monolith can be retired; until then the
`monolith` cluster and its catch-all must stay in the gateway.

## Known limitations

- ~~`/exercises/{id}/chat` and `/exercises/{id}/voice/stream` (interactive `ai_dialogue`)
  are served by Learning with the OpenAI chat + TTS pipeline ported from the monolith.~~
  **Closed in Phase 40.33.** The two routes are unchanged and so is `ExerciseDialogService` — what
  moved is where the completion and the synthesis are produced. `IOpenAiChatService` is now
  `AiChatClient` (`POST /ai/chat`, `POST /ai/chat/stream`) and `ITtsRouter` is now `AiTtsClient`
  (`POST /ai/tts`), both against ai-service; the in-process `OpenAiChatService`, `YandexTtsService`
  and `TtsRouter` are deleted, and **learning-service holds no provider key at all** —
  `OPENAI_API_KEY` and `YANDEX_TTS_API_KEY` were removed from its compose block and from
  `scripts/lib-local-env.sh`. This was the last door around the per-organization meter; see
  [AI_QUOTAS.md](AI_QUOTAS.md) §1 and `scripts/ai-provider-lint.py`, which now fails the build if a
  second one appears.
- `technique.mastery.changed` has a publisher and contract but no current trigger:
  the monolith's `MarkTechniqueSeen` only records first-seen and never changes mastery
  (matching prior behaviour). The producer is wired for when a mastery-progression
  flow lands.
- **A programme pin does not yet change what a learner sees on the existing screens (40.17).**
  `GET /skill-tree`, `/lessons` and `/exercises/*` still read the live content tree; the pinned
  programme is served by `GET /program` and nothing in the frontend reads it yet. The backend
  guarantee the phase makes is real and complete — no code path moves somebody's pin except their
  own explicit switch — but wiring the learner's existing screens onto the pinned programme belongs
  with the screens that render a programme, which is 40.20. Recorded in `docs/DONT_FORGET.md`.

## The admin content pipeline (Phases 40.27–40.28, second door 40.31)

The РОП pastes their material and gets a lesson, with a **stop in the middle**: the extracted
structure — product, ICP, objections, script stages, tone, glossary, banned claims — is shown back for
confirmation before a single exercise is generated. Full description:
[CONTENT_PIPELINE.md](CONTENT_PIPELINE.md); routes in
[API_CONTRACTS.md](API_CONTRACTS.md); the table in [DB_SCHEMA.md](DB_SCHEMA.md).

What belongs to this service, and why it is split the way it is:

- **The state.** `ContentGenerationJobs` is strict tenant data in learning-db — the material, the
  structure, the approval, and the lesson the run produced. It lives here because the run's *output*
  is a `Lesson`, its `Exercise` rows and a `LessonVersion`, all of them this service's tables, all
  written in the same transaction that marks the run complete.
- **Not the LLM calls.** Those are ai-service's `POST /ai/content/structure` and
  `/ai/content/generate`, internal and un-gatewayed like `POST /ai/evaluate`. The second synchronous
  learning → ai seam, and the reason it goes there is roadmap 40.33: per-organization LLM spend is
  enforced at the one point every call passes through, and generating a lesson is about to be the
  most expensive call in the product.
- **Since 40.33 both calls declare themselves batch** (`X-Ai-Workload: batch`) and carry
  `X-Organization-Id`, and the sweep asks `GET /ai/quota/preflight?workload=batch` **before** it
  claims a lease — the claim spends an attempt, so learning about the quota wall afterwards would
  fail a run for a reason that has nothing to do with it. The preflight only reads; the charge stays
  at the completion, so there is no double counting with the pipeline's own 60-item ceiling. If the
  preflight cannot be reached the sweep proceeds and lets the real gate decide, because the real gate
  is in ai-service and cannot be skipped by a network blip.
- **`ContentGenerationSweepService`** is the eighth entry in
  [BACKGROUND_JOBS.md §2.1](TENANCY/BACKGROUND_JOBS.md), per-organization iteration over a system
  enumeration. Both halves are minutes long, so neither runs inside the request that asks for it.
- **Generated content is ordinary content.** A `Lesson` with `OrganizationId` set (never global — the
  shared library's one authoring path is the seeder), `Exercise` rows, and a published `LessonVersion`
  snapshot. The eleven existing renderers play it and 40.18's override machinery applies to it, with
  no new code. It arrives **archived**: reviewing the generated exercises item by item is 40.32, and
  unreviewed model output must not appear in the team's live tree before then.
- **`PUT /admin/lessons/{id}` gained an optional `isArchived`** in the same block, because archiving
  had no reverse and an archived-on-arrival lesson would otherwise be stranded.
- **The sufficiency threshold is this service's decision (40.28).** ai-service supplies an opinion;
  `ContentSufficiencyInspector` decides. It runs twice — free, on the raw text before any call, and
  again on the extracted structure, which is the honest signal: what could actually be read out of
  the material is what says whether four good exercises can be built, and a model that returned an
  invented ICP over an empty deck is the same failure arriving later. The refusal is a **state**
  (`insufficient`) carrying a list of gaps, not an error, because it has to be arguable: `POST
  …/material` appends text and resumes the run, and `StructuredMaterialLength` is what keeps the
  resumed call from re-reading — and re-paying for — the deck that was already structured.
- **The reviewed structure is also how the organization profile gets filled (40.29), and that costs
  this service nothing.** `ContentGenerationJobs.Structure` is the profile's field list, field for
  field — 40.27 shaped it that way on purpose. 40.29 added the promotion as
  `POST /organizations/profile/draft/apply` in **organization-service**, taking the document in its
  request body from a client that has just read it off `GET /admin/content-generation/{jobId}`. No
  code here changed and none should: this service does not write another service's aggregate, and its
  `OrganizationProfileReplicas` row stays read-only in both directions.
  Two consequences worth knowing when reading a run:
  - **A run refused by 40.28 *after* structuring is still a usable profile source.** The structure is
    on the row, and a profile needs less than a lesson does — «нет ни одного возражения» blocks four
    good exercises and does not block knowing what the company sells. Only a run refused *before*
    structuring has nothing to promote.
  - **Promotion does not change the run.** There is no «promoted» flag, no state, and no timestamp.
    A run is disposable and a profile is not (docs/DECISIONS.md, 2026-08-18), and a run that recorded
    what a different service did with its output would be claiming an authority over that row which
    it deliberately does not have.
- **The pipeline has a second door since 40.31**, `POST /admin/team/skill-gaps/{stageKey}/content`.
  It creates a run of the same six states with the same worker, differing only in that its material is
  composed rather than pasted and that it carries `GapSourceRef`. **Nothing in the pipeline branches
  on that column** — it is read by the suggestion panel and copied by `POST /admin/assignments`, and
  the checkpoint, the sufficiency threshold, the attempts, the lease and the archived arrival are all
  identical. A block that had needed a second pipeline for the dashboard's button would have been a
  block that got the first one wrong.

## Batch content adaptation and AI content review (Phase 40.32)

The pipeline above builds a lesson out of a document. This is the other direction: take content that
already exists and either **rewrite a whole stage into the customer's voice** or **say what is
methodically wrong with what their РОП wrote by hand**. Full description:
[CONTENT_PIPELINE.md §6a](CONTENT_PIPELINE.md); routes in [API_CONTRACTS.md](API_CONTRACTS.md); the
two tables in [DB_SCHEMA.md](DB_SCHEMA.md).

What belongs to this service, and why it is shaped the way it is:

- **Both halves are one machine.** `ContentAdaptationJobs.Mode` is `tone_rewrite` or
  `quality_review`; everything else — the scope query, the lease, the per-item claim, the queue, the
  worker — is shared. They differ in the prompt and in whether an item carries anything applicable.
- **Not the LLM calls.** `POST /ai/content/rewrite` and `POST /ai/content/review`, internal and
  un-gatewayed, sharing the `/ai/content` prefix, the client and the timeout with 40.27's two.
- **`ContentAdaptationSweepService`** is the ninth entry in
  [BACKGROUND_JOBS.md §2.1](TENANCY/BACKGROUND_JOBS.md) and the seventh production
  `IgnoreQueryFilters()` call site. One LLM call per exercise, a few items per tick, each committed on
  its own; the batch carries the lease and the item carries the "already paid for" fact, so a batch
  interrupted at item forty resumes at forty-one.
- **The worker cannot write an `Exercise`, and that is the block.** It writes
  `ContentAdaptationItems` and its batch's status columns. The only code that turns a proposal into an
  edit is `ContentAdaptationJobService.AcceptItemAsync`, inside an organization administrator's
  request, aimed at one item by id. There is no bulk verb, deliberately.
- **The scope is collected through 40.18's read resolution**, so a lesson the organization has already
  forked contributes *their* exercises and one they have not contributes the library's — never both.
  Accepting a proposal against a global exercise **forks the lesson first**
  (`IContentOverrideService.CreateOverrideAsync`) and writes the body into the copy, because RLS
  cannot protect the global library: "global" is a null and the content policy admits it in its
  `WITH CHECK` clause.
- **Accepting publishes nothing.** It writes the draft `Exercise` row exactly as
  `PUT /admin/exercises/{id}` does; minting a `LessonVersion` per accepted sentence would produce a
  version history nobody can read, and 40.15's argument is that a version is a decision. The change
  reaches learners when somebody publishes.
- **Staleness is the accept-time guard.** The item stores the hash of the body the model was shown and
  accept recomputes it against the row about to be written. A mismatch is a 409: somebody edited that
  exercise after the model read it, and applying would discard their words. 40.18 refused to build a
  three-way merge of prose and grading criteria; refusing is the same answer one level down.
- **The review's vocabulary is this service's** (`ContentReviewFindingCodes`, seven codes). ai-service
  returns codes and a quoted fragment; the Russian sentence and the `blocking`/`advisory` severity are
  resolved here. That is 40.28's arrangement, and the reason it matters is that «сколько упражнений у
  этого клиента с неизмеримыми критериями» is then a query rather than a reading exercise.

## Local dev

`scripts/dev-learning.sh` (host port **5008**, db `learning` on the shared Postgres).
Run alongside `scripts/dev-ai.sh` (AI grading) and `scripts/dev-gateway.sh`.
See [docs/TESTING/LEARNING_SERVICE.md](TESTING/LEARNING_SERVICE.md).
