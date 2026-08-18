# The admin content pipeline — structure, stop, generate

**Status:** implemented (Phase 40.27, 2026-08-18), **API-only** — the screen is 40.20 and is waiting
on the owner's design.

The РОП pastes their material and gets a lesson. Between those two things the pipeline **stops** and
shows what it read — the product, who they sell to, the objections, the stages of their script, the
tone — and asks «всё верно? что убрать, что добавить?». Nothing is generated until somebody answers.

Parent docs: [LEARNING_SERVICE.md](LEARNING_SERVICE.md) (owns the state and the content),
[AI_SERVICE.md](AI_SERVICE.md) (owns the two LLM calls).
Siblings: [SEEDER.md](SEEDER.md) (the same shape for the **global** library),
[TENANCY/CONTENT_MODEL.md](TENANCY/CONTENT_MODEL.md) (what the generated lesson becomes).

---

## 1. Why the stop is the whole feature

`.claude/local-seed/seed.py` already does structure → generate in one run, for Sellevate's own
content, offline, by a person who wrote the material. The product version has a different user: a
head of sales who uploads a deck they did not write, for a team they do manage, and who will be shown
the result once.

Without the stop, one wrong reading is expensive twice over:

- **In work.** An objection the team never actually hears becomes three exercises about it. Removing
  it after generation means re-generating the lesson; removing it at the checkpoint is one click.
- **In money.** Every exercise generated from a wrong premise is tokens spent on something that gets
  thrown away. The generation call reads the **confirmed structure and not the material**, so the
  fifty-page deck is paid for once, and a re-generation after an edit costs the second call only.

The second point is also why the material is deliberately absent from the generation request: if it
travelled alongside the structure, the model would keep re-finding the objection the РОП deleted and
putting it back, and the checkpoint would be advisory rather than binding.

---

## 2. The states

```
        POST /admin/content-generation
                    │
                    ▼
             structuring ──────────(3 failed attempts)──────► failed
                    │                                            │
        (ai/content/structure)                                   │ POST …/retry
                    ▼                                            │ resumes the half
            awaiting_review  ◄── PUT …/structure ──┐             │ that failed
                    │           (the checkpoint)   │             │
        POST …/approve  ← the only transition no worker can make │
                    ▼                                            │
              generating ──────────(3 failed attempts)───────────┘
                    │
        (ai/content/generate → Lesson + Exercises + LessonVersion)
                    ▼
               completed
```

`awaiting_review` is not a convenience. **`CK_ContentGenerationJobs_Checkpoint` says in the database**
that a run may not be in `generating` without both a structure and an `ApprovedAt`, so a second writer
added later inherits the rule instead of having to remember it.

---

## 3. The API

All five routes are `RequireOrgAdmin` and carry `[TenantTransaction]`. The organization is never in a
route, a query string or a body — it comes from `ITenantContext`
([TENANCY.md §1.3](TENANCY/TENANCY.md)).

| Route | What it does |
|---|---|
| `POST /admin/content-generation` | Starts a run. `{title, material}`. 400 under 200 characters of material — a length, not a judgement; the real refusal («добавьте примеры возражений») is 40.28 |
| `GET /admin/content-generation?status=` | The runs, newest first. No material and no structure — both are documents |
| `GET /admin/content-generation/{jobId}` | One run **with** its structure. This is what the checkpoint screen polls |
| `PUT /admin/content-generation/{jobId}/structure` | The reviewer's edit. 409 outside `awaiting_review` |
| `POST /admin/content-generation/{jobId}/approve` | «Всё верно». Idempotent by state: approving a run that is already generating or finished returns it unchanged |
| `POST /admin/content-generation/{jobId}/retry` | Puts a failed run back into the half it failed in. 409 on anything else |

Full request/response shapes: [API_CONTRACTS.md](API_CONTRACTS.md).

The two LLM routes — `POST /ai/content/structure` and `POST /ai/content/generate` — are **internal**
service-to-service endpoints behind `InternalServiceAuthFilter` and are deliberately not exposed
through the gateway, exactly like `POST /ai/evaluate`.

---

## 4. The extracted structure

```jsonc
{
  "product":      "…",                                  // or null
  "icp":          "…",                                  // or null
  "tone":         "…",                                  // or null
  "objections":   [{ "text": "Дорого", "bestResponse": "…" }],
  "scriptStages": ["Приветствие", "Выявление потребности"],
  "glossary":     { "СДЭК": "…" },
  "bannedClaims": ["гарантированная доходность"]
}
```

**This is the organization profile's field list** ([CONTENT_MODEL.md §3](TENANCY/CONTENT_MODEL.md)),
field for field. That is not a coincidence and it is not an accident that it is a **separate
document** rather than the profile row — see [DECISIONS.md](DECISIONS.md) (2026-08-18). The short
version: a run is one upload and is disposable, a profile is the organization's standing truth that
40.19 renders into every base lesson, and one uploaded deck must not silently overwrite
`banned_claims` a compliance officer entered. Promoting a reviewed structure **into** the profile is
roadmap 40.29, and the identical shape is what makes it a copy rather than a translation.

The relationship does run one way already: structuring is **seeded** from the profile replica, so a
customer who has filled the form in is not asked the same seven questions again, and the model is
told to fill gaps rather than to contradict a human.

Two rules the prompts enforce and the caps that back them:

- **A gap is left as a gap.** `null` for a scalar, `[]` for a list. A fabricated ICP is
  indistinguishable, on the review screen, from an extracted one — and the checkpoint would ratify it.
- **Every value is bounded** at 2000 characters and every list is capped (10 objections, 12 script
  stages, 30 glossary terms, 20 banned claims), the same caps the 40.19 render path uses, so a value
  that survives extraction survives being put in a prompt.

---

## 5. What generation produces

One lesson, filed under a per-organization skill (`iconicName: ai-generated`, created on first use)
in a topic of its own named after the run.

- **Ordinary rows.** A `Lesson`, its `Exercise` rows, and a published `LessonVersion` snapshot — the
  same three things every other lesson is made of. The eleven existing renderers play it, the
  existing editor edits it, 40.18's override machinery applies to it and 40.16's progress binding
  works on it, with no new code. A second home for generated content would have been a second grading
  path and a second thing for 40.19's substitution to forget.
- **Owned, never global.** `OrganizationId` is the caller's. The shared library has exactly one
  authoring path and it is the seeder ([SEEDER.md §0](SEEDER.md)).
- **Archived on arrival.** The checkpoint this block buys sits *before* generation; whether the
  generated exercises are any good is a second question, and answering it item by item is roadmap
  40.32. Until somebody looks, unreviewed model output stays out of the team's live tree. The way out
  is the ordinary `PUT /admin/lessons/{id}` with `isArchived: false`, which 40.27 added because
  archiving had no reverse before it.
- **Four exercise types**, not eleven: `theory_card`, `choose_option`, `spot_mistake`, `free_text` —
  teach, recognise, diagnose, produce. Each schema has to be stated exactly in the prompt, and every
  one the model gets slightly wrong is an exercise `ExerciseContentValidator` drops on arrival: a paid
  call producing nothing. The other seven are reachable by hand.
- **Exercises that fail validation are dropped, never repaired**, and the count is logged and stored
  (`ProducedExerciseCount`). A run where *everything* failed validation is a **failure**, not an empty
  success — a "completed" run pointing at a lesson with no exercises looks finished and teaches
  nothing.
- **`banned_claims` binds the answer key.** 40.19 made the persona refuse to voice a banned claim and
  the grader refuse to reward one; here the third face of the same rule is that no `is_correct: true`
  option, no theory card and no grading criterion may contain one. An exercise whose *correct* answer
  is a forbidden promise does not merely permit the claim — it teaches and then rewards it. The block
  is appended last, after every block carrying the customer's own words, for the reason
  [AI_SERVICE.md](AI_SERVICE.md) gives: a rule a later block can qualify is not a rule.

---

## 6. The long operation

Both halves are minutes, not milliseconds, so neither runs inside the request that asks for it.
`ContentGenerationSweepService` is the eighth entry in
[BACKGROUND_JOBS.md §2.1](TENANCY/BACKGROUND_JOBS.md) — per-organization iteration over a system
enumeration, with the same `BYPASSRLS` footnote the seven jobs above it carry.

Three properties are worth keeping if this code is ever rewritten.

- **The claim is one conditional `UPDATE`, and it commits before the call.** A read-then-write claim
  under READ COMMITTED lets two instances both see a free lease and both stamp it — and both would
  then pay for the same generation. Committing before the call is what lets a five-minute call happen
  without an idle-in-transaction connection behind it; a rollback would not un-bill the provider
  anyway.
- **The lease outlives the HTTP timeout on purpose** (10 minutes against 300 seconds). A lease that
  expired while the call was still in flight would hand the run to a second worker and buy the lesson
  twice.
- **Cost is guarded by derived state, not by a counter.** A run holding a `ProducedLessonId` is never
  generated again, whatever else is true of it, and `CK_ContentGenerationJobs_Produced` makes that
  column meaningful by refusing to let it exist outside the `completed` state. Same rule 40.22 and
  40.24 followed: derive from state, never increment.

An attempt is spent per claim; three of them and the run is `failed` and waits for a person. A retry
resumes the half that failed rather than starting over — a failed generation must not re-pay for
structuring.

---

## 7. What this block deliberately did not do

- **No screen.** Same as 40.15–40.26: the РОП's admin panel is 40.20 and waits on the owner's design.
  See [ADMIN_PANEL.md](ADMIN_PANEL.md).
- **No refusal on thin input.** A 200-character floor is all there is; «загрузите ещё материал, не
  хватает примеров возражений» needs the model's opinion and is 40.28.
- **No promotion into the organization profile.** 40.29.
- **No file upload and no call recordings.** The material is pasted text. 40.30 owns recordings, and
  the consent and retention question it has to answer first.
- **No per-item accept/reject of generated exercises.** 40.32. The lesson arrives archived precisely
  because that gate does not exist yet.
- **No tests.** Rule №3 in [DONT_FORGET.md](DONT_FORGET.md) — what is missing and why it matters is
  listed there under «Тесты, которых нет».
