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

### Background jobs

| Job | Mode | Why it is safe |
|---|---|---|
| `OutboxRelayBackgroundService` | system | Reads `OutboxMessages` only, which has no `OrganizationId` filter and no RLS policy — the tenant travels in the envelope payload (TENANCY.md §1.7). The single legitimate cross-tenant reader. |
| `UserReplicaConsumer` | platform-global (`RequiresOrganization => false`) | Projects identity's cross-org user table; `UserReplicas` has no organization column. |
| `OrganizationProfileConsumer` (40.19) | tenant from the envelope (`RequiresOrganization` inherited `true`) | Projects `organization.profile.updated` into `OrganizationProfileReplicas`, which is strict tenant data — so the write happens under ordinary tenant context, with no RLS widening. The first consumer here that does *not* opt out. |
| `LessonVersionBackfill` (startup, once) | system | Mints "version 1" for never-published lessons; sees the global library only. |

There is no per-organization iteration job in learning-service. An **unset tenant is an exception,
never a licence**: `KafkaConsumerBackgroundService` throws when a consumer that requires an
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

The first three match the gamification-service consumer contract verbatim
(`ExerciseCompletedEvent`, `LessonCompletedEvent`, `SkillCompletedEvent`). Consumed by
Gamification (XP/streaks/achievements/league) and Analytics (`exercise.completed`).

## Events consumed

`user.registered` / `user.updated` / `user.avatar.changed` / `user.deleted` →
keep the local `UserReplica` in sync (idempotent, dedupe on `eventId`).

`organization.profile.updated` (Phase 40.19) → keep `OrganizationProfileReplicas` in sync, so
`{{organization.*}}` placeholders resolve without a call into organization-service on the read path
of every lesson. Full payload every time, so a dropped message is repaired by the customer's next
save rather than made permanent.

## Synchronous dependency

`Learning → AI`: `POST /ai/evaluate` for the 5 AI-graded exercise types
(`spot_mistake`, `rewrite`, `ai_dialogue`, `evaluate_call`, `free_text`). The learner
is waiting for the grade in real time, so this is REST, not an event. Configured via
the `AiService:BaseUrl` option (`http://ai:8080` in compose).

## Routes flipped at the gateway

`/skills/*`, `/skills`, `/skill-tree`, `/program`, `/program/*` (40.17), `/lessons/*`,
`/lessons`, `/topics/*`, `/exercises/*`, `/reference/*`, `/reference`, `/techniques/*`,
`/techniques`, `/daily-quote`, and the learning `/admin/*` content routes (`/admin/skills`,
`/admin/skill-stages`, `/admin/topics`, `/admin/lessons`, `/admin/exercises`,
`/admin/exercise-type-prompts`, `/admin/reference`, `/admin/techniques`,
`/admin/daily-quotes`, `/admin/seeder`, `/admin/program` and `/admin/program/*` — 40.17).
`/profile/*` is intentionally NOT captured (owned by identity/gamification).

After this flip the only public route still served by the monolith catch-all is
`/admin/users/*` (admin user management: list/detail, moderation rename, avatar
reset, role change). It was never part of any service's scope — Identity owns the
user aggregate but never took these admin routes. Phase 9 must move `/admin/users/*`
(naturally to identity-service) before the monolith can be retired; until then the
`monolith` cluster and its catch-all must stay in the gateway.

## Known limitations

- `/exercises/{id}/chat` and `/exercises/{id}/voice/stream` (interactive `ai_dialogue`)
  are served by Learning with the OpenAI chat + TTS pipeline ported from the monolith
  so the frontend contract is preserved. Long term this LLM/TTS compute belongs in
  ai-service behind a generic chat endpoint; that refactor is out of Phase 8 scope.
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

## Local dev

`scripts/dev-learning.sh` (host port **5008**, db `learning` on the shared Postgres).
Run alongside `scripts/dev-ai.sh` (AI grading) and `scripts/dev-gateway.sh`.
See [docs/TESTING/LEARNING_SERVICE.md](TESTING/LEARNING_SERVICE.md).
