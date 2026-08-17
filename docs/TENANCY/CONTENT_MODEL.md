# Per-organization content: customization, versioning, overrides

**Status:** §2.1, §2.2 and the `content_hash` rule are **implemented** (Phase 40.15, 2026-08-17) —
see [DB_SCHEMA.md](../DB_SCHEMA.md) (`LessonVersions`),
[SKILLS_AND_EXERCISES.md](../SKILLS_AND_EXERCISES.md) (part 3.5) and
[LEARNING_SERVICE.md](../LEARNING_SERVICE.md). Everything else on this page is still design only:
§2.3 (progress referencing the version) is 40.16, §2.5 (programme versioning) is 40.17, §2.6
(overrides and the staleness queue) is 40.18, §3 (the organization profile) is 40.19.

Two places where the implementation is narrower than the text below, deliberately. `is_breaking`
(§2.4) is recorded on every publish but nothing reads it yet — the dashboard that joins and splits
metric series is 40.16. And `parent_lesson_id` / `base_version_id` exist and are filled correctly
when set, but nothing creates an override: copy-on-write is 40.18, and creating copies earlier would
be the fork §1 exists to forbid.

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

### 2.4 `is_breaking`

A publish is flagged by the admin as cosmetic (typo, rewording) or semantic (the correct answer
changed, grading criteria changed).

The dashboard joins metric series across cosmetic versions and splits them across semantic ones.
Without this, the accuracy chart for a skill steps every time someone fixes a comma, and the РОП
stops trusting it — the same failure as §2.3, arrived at from the other direction.

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

### 2.6 Staleness, and no auto-merge

An override carries `base_version_id`. When the global lesson publishes a new version, every
override forked from an older base is marked **stale** and queued for the organization admin to
review.

Automatic merging of a customer-edited lesson with an upstream edit is not attempted. The content
is prose and grading criteria; a three-way merge produces plausible-looking nonsense that then
grades a salesperson.

The review queue shows: what changed upstream, what the organization changed, and three actions —
take the new base (discard the override), keep the override (re-point `base_version_id`), or edit.

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

**This is the metric that decides whether the architecture worked** (repeated from
[TENANCY.md §5](TENANCY.md#5-the-commercial-trap-this-architecture-has-to-defuse)): on the first
pilot, measure the share of adaptation closed by profile substitution versus hand-editing lesson
text. Above one third hand-edited → the parameterization is wrong, and it is cheap to fix now and
expensive at ten customers.

---

## 4. Implications for what already exists

| Existing thing | What tenancy does to it |
|----------------|--------------------------|
| `seed.py` + `/admin/seeder/bundle` | Seeds the **global** library (`organization_id IS NULL`). Needs an explicit target, and must not be pointed at a customer. |
| `Skill.IconicName` uniqueness | Becomes unique per organization — see [TENANCY.md §1.9](TENANCY.md#19-indexes-and-unique-constraints) |
| `DialogMode` / `DialogBundle` prompts | Already admin-editable; become override-able per organization. The seeded hidden modes (`company-call`, `custom-scenario`) stay global. |
| `ExerciseTypePrompt` | Stays platform-global — it defines how a *type* is graded, not what a customer teaches |
| `Technique`, `ReferenceMaterial` | Same override + versioning treatment as lessons |
| Admin panel ([ADMIN_PANEL.md](../ADMIN_PANEL.md)) | Splits into a platform superadmin panel (organizations, global library) and an organization admin panel (the РОП's) |
