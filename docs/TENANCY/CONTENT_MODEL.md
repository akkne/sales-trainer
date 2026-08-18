# Per-organization content: customization, versioning, overrides

**Status:** §2.1, §2.2 and the `content_hash` rule are **implemented** (Phase 40.15, 2026-08-17);
§2.3 (progress referencing the version) and §2.4 (`is_breaking`) are **implemented** (Phase 40.16,
2026-08-17); §2.5 (programme versioning and enrollment) is **implemented** (Phase 40.17,
2026-08-17) — see [DB_SCHEMA.md](../DB_SCHEMA.md) (`LessonVersions`, `UserExerciseAttempts`,
`UserLessonProgressRecords`, `ProgramVersions`/`ProgramItems`/`ProgramEnrollments`),
[SKILLS_AND_EXERCISES.md](../SKILLS_AND_EXERCISES.md) (parts 3.5 and 3.7),
[LEARNING_SERVICE.md](../LEARNING_SERVICE.md) and [ANALYTICS_SERVICE.md](../ANALYTICS_SERVICE.md)
(how metrics are counted per version); §1 and §2.6 (copy-on-write overrides, read resolution and the
staleness queue) are **implemented** (Phase 40.18, 2026-08-18) for lessons, techniques, reference
materials and dialog-mode prompts; §3 (the organization profile and placeholder substitution) is
**implemented** (Phase 40.19, 2026-08-18) — see [CONTENT_PARAMETERIZATION.md](../CONTENT_PARAMETERIZATION.md)
for the syntax and [SEEDER.md](../SEEDER.md) §0 for what changed in the seeder.

Three places where the implementation is narrower than the text below, deliberately.

- The three review actions exist as API; **the review screen does not.** The frontend was not touched
  in 40.18 for the same reason it was not touched in 40.15–40.17 — the РОП's admin panel is 40.20 and
  is waiting on the owner's design. See `docs/DONT_FORGET.md`.
- The §2.4 dashboard is an API (`GET /admin/lessons/{lessonId}/accuracy`), not a screen. The screen
  belongs with the РОП's admin panel, which is 40.20.
- An edit that is **not** published binds new attempts to the previous snapshot. The attempt-time
  resolver mints a version only when a lesson has none at all, never on unpublished drift, because
  an unattributed content change would have to be treated as breaking and would then split the
  series on every fixed comma — the §2.4 failure reached from the other side. Publishing is the act
  that makes an edit historically visible, and 40.20's editing screen has to make it the natural end
  of editing. See `docs/DECISIONS.md` (2026-08-17) and `docs/DONT_FORGET.md`.
- The §2.5 pin is enforced end to end in the backend, but **the learner's existing screens do not
  read it yet**. `GET /skill-tree`, `/lessons` and `/exercises/*` still serve the live library; the
  pinned programme is `GET /program` and nothing in the frontend calls it. So the guarantee "nobody
  moves a learner's programme but the learner" is real and complete, while "the learner sees the
  pinned programme" waits on the screens that render one, which is 40.20.

Parent doc: [TENANCY.md](TENANCY.md). Sibling: [ASSIGNMENTS.md](ASSIGNMENTS.md).
Current content model: [SKILLS_AND_EXERCISES.md](../SKILLS_AND_EXERCISES.md),
[NEW_EXERCISE_TYPES.md](../NEW_EXERCISE_TYPES.md).

---

## 0. What the content model actually looks like today

This matters, because the "version the lesson body" design has to be adapted: **a `Lesson` has no
body.**

```csharp
// learning-service
Skill    { Id, IconicName, OrderInTree, Title, Description, Stage }
Topic    { ... }                       // Skill → Topic
Lesson   { Id, TopicId, OrderInTopic, Title }          // ← that is the whole entity
Exercise { Id, LessonId, Type, OrderInLesson, SerializedContent, CustomAiPrompt, ... }
```

All actual content — question text, options, correct answers, theory cards, grading criteria —
lives in `Exercise.SerializedContent` (JSON) and `Exercise.CustomAiPrompt`. A lesson is an ordered
container of exercises and a title.

Consequences for the design below:

- The **versioned unit is the lesson together with its full ordered set of exercises**, snapshotted
  as one JSON document. Not the `Lesson` row alone (nearly empty), not each exercise separately
  (see §2.3).
- Two other content families need the same treatment and are easy to forget: `Technique`
  (+ `TechniqueSkill`, `TechniqueCoach`) and `ReferenceMaterial`, plus `DialogMode`/`DialogBundle`
  prompt templates in ai-service, which are already admin-editable and are exactly what a customer
  will want to tune first.

---

## 1. Do not copy the curriculum per customer

The obvious onboarding move — clone the whole programme into the new organization so they can edit
it — is the one that has to be rejected explicitly, because it is what everyone does by default and
it is unrecoverable once there are customers on it.

After 15 customers there are 15 forks. Improving a base lesson reaches nobody. The content roadmap
stops existing, and every base fix becomes 15 merges by hand.

Instead, three separations:

| Layer | What it is | Customizable |
|-------|-----------|--------------|
| **Programme structure** | An ordered list of references: which skills, which lessons, in what order, what is hidden, what is added | Freely — reordering touches no lesson |
| **Lesson body** | Either global (`organization_id IS NULL`) or an override with `parent_lesson_id` | Copy-on-write, only when an admin actually edits |
| **Organization profile** | Product, ICP, objections, script, tone | A form — and this is where most customization should land |

Read resolution: an override exists → use it; otherwise → the global lesson. A copy is created at
the moment the admin presses "edit", never at onboarding.

---

## 2. Immutable lesson versioning

### 2.1 The tables

```
lesson                       -- the identity/lifeline of a lesson
  id
  organization_id            -- NULL = global base library
  parent_lesson_id           -- set when this is an org override of a global lesson
  slug
  archived
  UNIQUE (organization_id, slug)

lesson_version               -- an immutable snapshot
  id
  lesson_id
  version_no
  content jsonb              -- title + the full ordered exercise set, denormalized
  content_hash
  status                     -- draft | published | archived
  base_version_id            -- which global version this override was forked from
  is_breaking
  created_by, created_at, published_at
```

Rules:

- Editing happens on a **`draft` row, which is mutable**. Publishing freezes it: `status =
  published`, and that row is never modified again.
- The next edit creates a new `draft` by copying the last published version.
- `content_hash` prevents a new version when publish is pressed with no actual change.
- Only one `draft` per lesson at a time — otherwise two admins produce two forks and there is no
  merge story. Enforce with a partial unique index:
  `CREATE UNIQUE INDEX ... ON lesson_version (lesson_id) WHERE status = 'draft'`.

### 2.2 Why the snapshot is denormalized JSON

`Exercise` rows stay as the *working* representation the admin panel edits. On publish, the lesson
and its exercises are serialized into `lesson_version.content`. Reads of historical data go to the
snapshot; the editor works on rows.

This avoids the alternative — versioning every `Exercise` row and reconstructing a lesson from N
version rows — which turns every historical query into an archaeology exercise. Lessons are
kilobytes. Copy the whole thing.

### 2.3 Progress must reference the version

This is the concrete bug in the current schema:

```csharp
public sealed class UserExerciseAttempt
{
    public Guid ExerciseId { get; set; }   // ← points at mutable content
    public bool IsCorrect { get; set; }
    public int Score { get; set; }
}
```

An admin fixes a wrong correct-answer, and every historical attempt silently re-interprets against
new content. Accuracy-per-skill — the number sold to the РОП as a measure of team readiness —
changes retroactively. Once a customer notices this once, they stop believing the dashboard, and
they are right to.

So attempts and lesson progress carry `lesson_version_id` (and the exercise's identity **within**
that version), not a bare `exercise_id`.

**As implemented (40.16):** one new nullable column, `LessonVersionId`, on both tables. The
exercise's identity within the version needs no column of its own — `exerciseId` is already inside
the snapshot and inside its hash (§2.2), so `ExerciseId` stays and changes meaning rather than shape:
a key into a frozen document instead of a pointer at an editable row. Nullable because attempts
recorded before the phase have nothing to point at until the backfill runs, and no foreign key
because a content table under an `IS NULL OR = current` policy and strict tenant data under plain
equality must not be joined by a constraint validated with the writer's privileges.

### 2.4 `is_breaking`

A publish is flagged by the admin as cosmetic (typo, rewording) or semantic (the correct answer
changed, grading criteria changed).

The dashboard joins metric series across cosmetic versions and splits them across semantic ones.
Without this, the accuracy chart for a skill steps every time someone fixes a comma, and the РОП
stops trusting it — the same failure as §2.3, arrived at from the other direction.

**As implemented (40.16):** `GET /admin/lessons/{lessonId}/accuracy` in learning-service, which owns
the attempts. A segment starts at the first published version and at every version flagged breaking;
cosmetic versions extend the segment before them. Attempts with no version are a separate
`unversionedAttempts` bucket, never folded into version 1 — nobody can prove what they were scored
against. analytics-service computes none of this and is not going to: it is Redis-only, stores no
attempts, and its `exercise.completed` counter is a platform-wide funnel number with no lesson, no
version and no organization in it. The rule for anyone drawing the chart lives in
[ANALYTICS_SERVICE.md](../ANALYTICS_SERVICE.md).

### 2.5 Programme versioning

```
program_version
  id, organization_id, version_no, status
program_item
  program_version_id, skill_id, order_index, lesson_version_id   -- pinned to a concrete version
enrollment
  user_id, program_version_id                                    -- the learner is pinned to a snapshot
```

A manager on lesson 8 of 21 must not find the programme rearranged underneath them. New enrollments
go to the new version; existing learners are offered an explicit "switch to the current version"
with a diff.

**As implemented (40.17):** `ProgramVersions`, `ProgramItems`, `ProgramEnrollments` in learning-db,
next to the lessons and lesson versions they reference. Six things worth stating, because each is a
fork the roadmap left open (full reasoning in `docs/DECISIONS.md`, 2026-08-17).

- **All three are strict tenant data**, not content. There is no global programme: a curriculum is a
  decision one organization made about its own people, so `organization_id` is `NOT NULL` and the RLS
  policy is plain equality rather than the `IS NULL OR = current` every table above uses. This is the
  first place in Stage D where the content flavour would have been actively wrong.
- **`program_item` carries `lesson_id` as well as `lesson_version_id`.** Not decoration: without it,
  "the same lesson, now pinned to a newer snapshot" is inexpressible, and a curriculum could list one
  lesson at versions 3 and 5 at once — the same material with two answer keys. It cannot drift,
  because a published version's `lesson_id` is frozen by §2.1's trigger. The pin also survives §2.6
  re-pointing `base_version_id`, since that column is provenance and the version's identity is not.
- **The structure is frozen by a trigger on `program_item`, not only on `program_version`.** The
  structure lives in the item rows, so that is where a retroactive reorder would actually be written,
  and deleting an item from a frozen programme is the same edit seen from the other side. The reason
  to put it in the database is sharper than it was for lessons: an edited lesson snapshot corrupts a
  metric, an edited programme rearranges the curriculum under somebody mid-course.
- **Enrollment is asymmetric.** The administrator's enroll call is idempotent and never moves an
  existing pin, so re-running it after a publish enrolls the newcomers and leaves everybody
  mid-course alone. The switch is the learner's own call, on themselves, naming the target version so
  that a publish between showing the diff and accepting it cannot redirect them. No route moves
  somebody else's pin — the claim in the paragraph above is about which code paths exist.
- **The diff has four buckets** — added, removed, re-pinned, moved — and `is_breaking` on a re-pinned
  lesson reads every published version between the two pins rather than the target's own flag. A
  programme can skip several lesson versions at once, and reading only the target would hide a
  changed correct answer behind a later typo fix: the §2.4 failure one level up.
- **Enrollment does not gate access, and no "programme version 1" is minted.** An organization that
  has published nothing has no pins and its people read the live library exactly as before. 40.16
  could mint a lesson's version 1 because the lesson body existed and only its snapshot was missing;
  a programme version is not a snapshot of something that exists but a curriculum decision nobody has
  made yet, and pinning every existing learner to whatever the seeder loaded would freeze them onto
  it silently.

### 2.6 Staleness, and no auto-merge

An override carries `base_version_id`. When the global lesson publishes a new version, every
override forked from an older base is marked **stale** and queued for the organization admin to
review.

Automatic merging of a customer-edited lesson with an upstream edit is not attempted. The content
is prose and grading criteria; a three-way merge produces plausible-looking nonsense that then
grades a salesperson.

The review queue shows: what changed upstream, what the organization changed, and three actions —
take the new base (discard the override), keep the override (re-point `base_version_id`), or edit.

**As implemented (40.18):** eight things worth stating, because each is a fork the roadmap left open
(full reasoning in `docs/DECISIONS.md`, 2026-08-18).

- **A copy is made only when an administrator presses "edit".** `POST
  /admin/content/overrides/{kind}/{baseId}` is the only code path in either service that creates one.
  Nothing runs at onboarding, on a Kafka event or on a schedule — the verify script's last check
  simply counts the copies, and on a fresh deployment the answer is zero.
- **"Stale" is not stored anywhere.** The queue is a query that compares each override's fork marker
  against the base as it stands right now. Marking synchronously at publish time is not merely
  awkward, it is refused by the database: it would mean writing rows into organizations the publisher
  is not in, and the RLS `WITH CHECK` clause is the one clause the 2026-08-16 role split deliberately
  did not widen. A background sweep would work and was rejected for lagging: while it lags, the queue
  says an override is current when its base has already moved, which is the one error a review queue
  must not make.
- **The fork marker is a version id for lessons and a content fingerprint for the other three.**
  `Technique`, `ReferenceMaterial` and `DialogMode` have no immutable version table, and building
  three more was out of scope. The fingerprint answers "has upstream moved?" exactly as well; what it
  gives up is the before-image, so the review payload's `baseAtFork` is populated for lessons and null
  for the rest.
- **The API computes no diff at all**, only returns the documents. A textual diff of prose is the
  first half of a merge, and the pressure to "apply the non-conflicting hunks" starts the moment one
  exists.
- **Read resolution is an explicit call, and only on learner-facing paths.** The query filter admits
  "mine or global", so without it an organization sees every overridden lesson twice. The authoring
  paths deliberately keep seeing both sides — the review screen exists to show them side by side —
  and platform-wide callers do not resolve either, or one customer's edit would hide a global lesson
  from Sellevate staff.
- **Retiring an override archives it, never deletes it.** Progress rows and Mongo dialog sessions
  point at these rows without a foreign key, so deleting one to tidy a queue orphans history.
  `IsArchived` was added to `Techniques` and `ReferenceMaterials` to match `Lessons`; in ai-service
  the existing `IsActive` does the job.
- **The write boundary is in C#, not in RLS.** The content policy admits a null owner on write by
  design (the seeder and every platform authoring path need it), which read as a write rule says any
  organization may edit the shared library. `ContentAuthoringGuard` states the real rule once, and
  three CHECK constraints say in the database that an override always has an owner.
- **`DialogBundle` is not copy-on-write, and that is the one place the implementation narrows the
  roadmap.** A bundle carries no prompt; a copied one is an empty folder needing a second resolution
  layer for "which modes are in it", whose natural answer is §1's library fork one level down. Only
  `DialogMode` — which carries `ChatSystemPrompt` and `FeedbackSystemPrompt` — is override-able.

---

## 3. The organization profile — the part that removes most forks

The largest share of "customization" is not structural. It is that the lesson says «ваш продукт»
and the customer wants it to say their product, with their objections and their tone.

That does not need a fork. It needs substitution.

```
organization_profile
  organization_id PK
  product              -- what they sell, in prose
  icp                  -- who they sell to, deal size, cycle length
  objections jsonb     -- [{text, frequency, best_response}]
  script jsonb         -- their call stages
  tone                 -- formal / peer-to-peer / consultative
  glossary jsonb       -- internal terms, product names, competitor names
  banned_claims        -- what a rep must never promise (legal/compliance)
```

Base lessons and dialog persona prompts are written with placeholders resolved from this profile at
render time. One base lesson serves every customer; the customer fills in a form.

`banned_claims` is worth calling out separately: in regulated industries (finance, medicine) the
customer will ask what stops the AI persona from coaching a rep into an illegal promise. Having an
answer is a sales asset.

**As implemented (40.19):** six things worth stating, because each is a fork the roadmap left open
(full reasoning in `docs/DECISIONS.md`, 2026-08-18; syntax and authoring guidance in
[CONTENT_PARAMETERIZATION.md](../CONTENT_PARAMETERIZATION.md)).

- **The syntax is `{{organization.<field>}}`, and substitution happens on read, never on write.** The
  row and the §2.1 snapshot both keep the template; only the HTTP response and the outgoing AI prompt
  carry substituted text. Rendering before the write would freeze a different snapshot — and a
  different `content_hash` — per organization for the same base lesson, which is §1's fork reached by
  accident and stripped of §2.6's guard rails. The same argument protects `DialogMode`'s 40.18
  fingerprint: a rendered prompt would make every override permanently stale.
- **An unfilled field renders as neutral prose, not as a blank and not as the raw placeholder.**
  «ваш продукт», «ваш клиент», «типичные возражения ваших клиентов» — the phrases the base library was
  already written in, so a trial account on day one reads exactly as it did before this phase. A
  visible `{{organization.icp}}` is a defect a salesperson sees; a blank produces «Расскажите, чем
  помогает », which reads as a broken product rather than an empty form. An unknown key (a typo) is
  removed and logged rather than displayed. What this buys is paid for in Russian grammar: there is no
  declension engine and there is not going to be one, so base sentences have to be phrased to survive
  the fallback.
- **The grader renders too.** Not symmetry for its own sake: a question rendered for the learner and
  unrendered for the grader marks correct answers wrong, because the deterministic strategies compare
  option text and the AI strategy would be judging an answer to a question it was not shown.
- **`banned_claims` binds both the persona and the scoring.** ai-service's chat prompt, ai-service's
  feedback prompt, and learning-service's exercise grading prompt, all from one builder in
  BuildingBlocks. Enforcing only the persona side is worse than nothing: a persona that stays silent
  while the grader keeps rewarding the forbidden claim teaches the rep to say it anyway. The block is
  appended **last**, after every block carrying human-written text, because a rule something later can
  qualify is not a rule.
- **The profile reaches the two rendering services as a replica, not a call.** `organization.profile.updated`
  on Kafka → `OrganizationProfileReplicas` in learning-db and ai-db, the same shape as `UserReplicas`
  (40.2) and `OrganizationReplicas` (40.9). Substitution sits on the read path of the entire product,
  and a synchronous hop there would mean lessons go down when organization-service does, to deliver
  something whose absence is merely cosmetic. The cost is eventual consistency; the payload is the
  whole profile every time, so a lost message is repaired by the next save. Unlike every earlier
  replica consumer, these two run in **tenant** mode — the profile is inside a tenant, not about one.
- **Platform-wide callers get the empty profile.** In platform mode the query filter admits every
  organization at once, so "the profile" is undefined and picking a row would render Sellevate staff a
  lesson with some customer's product name in it. The same rule §2.6's read resolution follows.

**As implemented (40.29): the profile is filled in by interview, not by that form.** The paragraph
above says «the customer fills in a form», and the form is thirty-odd inputs. The observation the block
starts from is that it therefore stays empty, and an empty profile is not a degraded version of this
section — it is this section not happening at all. What was added is four routes on the same row and
no schema change: a capped, ordered list of what is still missing
(`GET /organizations/profile/gaps`, three questions at a time), a per-field answer
(`PATCH /organizations/profile`), and the promotion of the structure the 40.27 pipeline extracted from
the customer's own deck and script (`POST /organizations/profile/draft`, `…/draft/apply`). The merge
policy is *fill blanks, grow lists, never silently replace a human's words* — and `banned_claims` is
union-only with no way to delete an entry through that path, which is what makes the guarantee two
paragraphs above survive somebody pasting a marketing deck in June. Full description in
[ORGANIZATION_SERVICE.md](../ORGANIZATION_SERVICE.md#the-profile-as-an-interview-phase-4029), decisions
in [DECISIONS.md](../DECISIONS.md) (2026-08-18).

**This is the metric that decides whether the architecture worked** (repeated from
[TENANCY.md §5](TENANCY.md#5-the-commercial-trap-this-architecture-has-to-defuse)): on the first
pilot, measure the share of adaptation closed by profile substitution versus hand-editing lesson
text. Above one third hand-edited → the parameterization is wrong, and it is cheap to fix now and
expensive at ten customers.

---

## 4. Implications for what already exists

| Existing thing | What tenancy does to it |
|----------------|--------------------------|
| `seed.py` + `/admin/seeder/bundle` | **Done (40.19).** Seeds the **global** library (`organization_id IS NULL`), with a required `target=global` field and every read narrowed to `organization_id IS NULL` — see [SEEDER.md](../SEEDER.md) §0. The narrowing was a real fix, not paperwork: reads went through the "mine or global" query filter, and lessons upsert on `(topicId, title)`, so re-running a bundle import could silently overwrite a customer's override with the base text. |
| `Skill.IconicName` uniqueness | Becomes unique per organization — see [TENANCY.md §1.9](TENANCY.md#19-indexes-and-unique-constraints) |
| `DialogMode` / `DialogBundle` prompts | **Done (40.18)** for `DialogMode`, which is where the prompts live: `ParentModeId` + `BaseContentHash`, the override keeping its parent's `BundleId` and `Key`. `DialogBundle` is not copy-on-write — see §2.6. The seeded hidden modes (`company-call`, `custom-scenario`) stay global and the service **refuses** to override them: their prompts are completed at run time from placeholders the code supplies. |
| `ExerciseTypePrompt` | Stays platform-global — it defines how a *type* is graded, not what a customer teaches |
| `Technique`, `ReferenceMaterial` | **Override done (40.18)**: `ParentTechniqueId` / `ParentMaterialId`, `BaseContentHash`, `IsArchived`, read resolution and the same review queue as lessons. **Versioning not done** — neither has an immutable version table, and the fork point is a fingerprint instead (§2.6). |
| Admin panel ([ADMIN_PANEL.md](../ADMIN_PANEL.md)) | Splits into a platform superadmin panel (organizations, global library) and an organization admin panel (the РОП's) |
| Where an organization's *own* lessons come from | **Done (40.27):** the admin content pipeline ([CONTENT_PIPELINE.md](../CONTENT_PIPELINE.md)) — material in, a structure the РОП confirms, then a `Lesson` + `Exercise` rows + a published `LessonVersion`, `organization_id` set and archived until reviewed. It is §1's third row (the profile) reached from the other end: what a profile cannot express, a customer now generates rather than forks |

---

## 5. Generated content (Phases 40.27–40.28)

The one thing worth stating here rather than only in [CONTENT_PIPELINE.md](../CONTENT_PIPELINE.md):
**generated content is not a fourth kind of content.** A run produces the same three things every
lesson in the product is made of — a `Lesson` row, `Exercise` rows, a published `LessonVersion`
snapshot — so §2's versioning, §2.6's override machinery and §3's substitution all apply to it with no
new code, and the eleven existing renderers play it.

Four consequences follow from that, and each one was a fork.

- **It is owned, never global.** `organization_id` is the caller's. The shared library has exactly one
  authoring path and it is the seeder ([SEEDER.md §0](../SEEDER.md)); a pipeline that could write a
  null owner would be that rule's back door.
- **It is not an override.** `parent_lesson_id` is null: the lesson was written from the customer's
  own material and forked nothing, so there is no base to go stale against and it never enters §2.6's
  review queue. That is the difference between "we adapted your version of our lesson" and "we made
  you a lesson".
- **It arrives archived.** §1's argument is about not forking the curriculum; this is the adjacent
  worry, which is not forking *quality*. The checkpoint 40.27 buys sits before generation, so whether
  the generated exercises are any good is still unanswered when the lesson is written, and 40.32 owns
  answering it item by item. Until then the lesson exists, is versioned and is addressable, and
  learners do not see it. Un-archiving is `PUT /admin/lessons/{id}` with `isArchived: false`, which
  40.27 added because archiving had no reverse before it.
- **Sometimes it is not written at all (40.28).** A run whose material was too thin — or whose
  extracted structure came back with no objections, no script stages and nothing about the product —
  ends in `insufficient` with a list of what to add, and writes no content. That is the same argument
  as the two above, applied one step earlier: the risk §1 guards against is a customer's tree filling
  with forks of our curriculum; this guards against it filling with lessons generated from nothing.
  A `Lesson` row is cheap to create and expensive to be wrong about — it is versioned, assignable and
  reportable — so a run with nothing to say produces no row rather than an empty one.
