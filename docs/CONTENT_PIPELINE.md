# The admin content pipeline — structure, stop, generate

**Status:** implemented (Phases 40.27–40.28, second entry point 40.31, 2026-08-18), **API-only** —
the screen is 40.20 and is waiting on the owner's design.

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
                    │ 40.28: the free check on the raw text
                    ├───────────────────────────────► insufficient ◄────┐
                    ▼                                     │  ▲          │
             structuring ──────(3 failed attempts)──► failed │          │
                    │                                     │  │          │
        (ai/content/structure, + sufficiency verdict)      │  │          │
                    │                                POST …/material    │
                    ├── 40.28: structure too thin ────────┘  │          │
                    ▼                                        │          │
            awaiting_review  ◄── PUT …/structure ────────────┘          │
                    │           (the checkpoint)                        │
        POST …/approve  ← the only transition no worker can make ───────┘
                    │           (409 + insufficiency if the structure is thin)
                    ▼
              generating ──────────(3 failed attempts)──────► failed
                    │                                            │
        (ai/content/generate → Lesson + Exercises + LessonVersion)  POST …/retry
                    ▼                                         resumes the half
               completed                                        that failed
```

`awaiting_review` is not a convenience. **`CK_ContentGenerationJobs_Checkpoint` says in the database**
that a run may not be in `generating` without both a structure and an `ApprovedAt`, so a second writer
added later inherits the rule instead of having to remember it.

`insufficient` (40.28) is not a failure. Nothing broke — the material was thin, and the run says
exactly what is missing. It is a state rather than an error precisely so that it can be argued with:
see §4a.

---

## 3. The API

All seven routes are `RequireOrgAdmin` and carry `[TenantTransaction]`. The organization is never in a
route, a query string or a body — it comes from `ITenantContext`
([TENANCY.md §1.3](TENANCY/TENANCY.md)).

| Route | What it does |
|---|---|
| `POST /admin/content-generation` | Starts a run. `{title, material}`. 400 only on an empty textarea or over 60 000 characters; **thin material is a run in `insufficient`, not an error** (40.28) |
| `GET /admin/content-generation?status=` | The runs, newest first. No material and no structure — both are documents. The refusal *is* carried: it is why the run is sitting there |
| `GET /admin/content-generation/{jobId}` | One run **with** its structure. This is what the checkpoint screen polls |
| `PUT /admin/content-generation/{jobId}/structure` | The reviewer's edit. Allowed at the checkpoint and on a refused run (40.28); the result is re-inspected. 409 elsewhere |
| `POST /admin/content-generation/{jobId}/material` | **40.28.** «Вот ещё материал» — appends and resumes. 409 unless the run is `insufficient` |
| `POST /admin/content-generation/{jobId}/approve` | «Всё верно». Idempotent by state: approving a run that is already generating or finished returns it unchanged. 409 + `insufficiency` if the structure is too thin |
| `POST /admin/content-generation/{jobId}/retry` | Puts a failed run back into the half it failed in. 409 on anything else |

Full request/response shapes: [API_CONTRACTS.md](API_CONTRACTS.md).

The two LLM routes — `POST /ai/content/structure` and `POST /ai/content/generate` — are **internal**
service-to-service endpoints behind `InternalServiceAuthFilter` and are deliberately not exposed
through the gateway, exactly like `POST /ai/evaluate`.

### 3a. The second door (Phase 40.31)

There is one more way in, and it belongs to a different feature: `POST
/admin/team/skill-gaps/{stageKey}/content`, the button by which the РОП's dashboard proposes
generating exercises for the funnel stage its heat map says the team is failing
([ASSIGNMENTS.md §3.4](TENANCY/ASSIGNMENTS.md)).

It creates an ordinary run. Same six states, same worker, same lease, same checkpoint, same
sufficiency threshold, same archived arrival. **Nothing in this pipeline branches on where a run came
from**, and that is the whole point of listing it here: the block that needed a button did not need a
second pipeline. Two differences, both outside the state machine:

- **The material is composed, not pasted.** There is no textarea behind that button, so
  `sourceMaterial` is written deterministically from the measurement (which stage, what accuracy, over
  how many attempts, how many managers below the bar, and the stage's weakest skills) followed by the
  organization profile — §4's field list as plain readable Russian. It is stored verbatim like any
  other material and shown back at the checkpoint, so «откуда это взялось» has the same kind of answer
  it always had. An organization with an empty profile gets a run in `insufficient` carrying §4a's own
  codes, which is correct rather than broken: we do not know enough about that company to write
  exercises for them, and the refusal names what to add.
- **The run carries `gapSourceRef`** — `skill-gap:<stage>@<yyyy-MM-dd>`. It is read by the suggestion
  panel, which will not offer a stage that already has a live run (and returns *that* run if the
  button is pressed again, rather than buying a second lesson), and it is copied into
  `Assignment.SourceRef` with `source_type = gap_detected` when `POST /admin/assignments` is called
  with `contentGenerationJobId`.

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

## 4a. The sufficiency threshold (Phase 40.28)

The product problem is reputational, and it is stated best by the roadmap: **a РОП who uploads a
three-slide deck and gets fifteen bland exercises blames the product, not their deck.** They are not
wrong to — we are the ones who chose to answer. Four good exercises, or an honest «добавьте примеры
возражений или запись звонка», are both better outcomes than fifteen bland ones.

So the pipeline is allowed to say no. **The refusal must be a useful answer, not an error 400.**

### Two stages, because neither one alone is honest

| Stage | Where | Cost | What it can see |
|---|---|---|---|
| `material` | learning-service, on `POST` and on every `POST …/material` | free | how much text there is (~400 characters / 60 words), and whether a single word in it belongs to selling |
| `structure` | learning-service, after the structuring call returns | already paid for | what could actually be **read** out of the material, plus the model's verdict, which rode the same completion |

The deterministic stage cannot tell three slides about a CRM from three pages of a recipe on length
alone — which is exactly why the lexical check is there. A document about selling that contains no
word about selling does not exist, and establishing that costs nothing. The test is **zero hits, not
a ratio**: a ratio would be a quality score and would start refusing unusual but perfectly good
material. A false positive is survivable and self-correcting, because the refusal says what to add
and one sentence about what they sell clears it — which is the only reason a lexical rule is allowed
to block anything.

**The structure stage is the more honest signal**, and it is the answer to «порог не должен быть
обходимым случайно». Length is a proxy. What decides whether four good exercises can be built is what
came back: no objections **and** no script stages means there is a topic and no task; no product
**and** no ICP means there is nothing to ground it in. A model that returned an invented ICP over an
empty deck is the same failure arriving later, and judging the artefact rather than trusting it is
what catches it.

### The model's opinion is free, and it can only tighten the gate

`POST /ai/content/structure` returns `{structure, sufficiency}` — one completion, two answers about
the same reading. A separate cheap «это про продажи?» call was rejected: this one already reads the
whole material and forms the judgement anyway.

The verdict may **add** a refusal and may never lift one. «Выглядит достаточно» over an empty
structure must not open the gate, or the threshold is bypassed by whichever completion happens to be
confident. Symmetrically, a bare «недостаточно» with no codes is **ignored**: an unactionable refusal
is the one thing this block must never produce, so a model that will not say what is missing is
treated as having no opinion.

### The refusal is arguable, and arguing with it is cheap

This is why it is a state and not a status code.

- `POST …/material` appends text and puts the run back to `structuring`. The next call reads **only
  the added part** — `StructuredMaterialLength` records how much has been paid for — alongside the
  structure already extracted, which the prompt is told to keep rather than rewrite. A РОП answering
  «нет ни одного возражения» pays for reading their objections list, not for reading the fifty-page
  deck a second time.
- `PUT …/structure` is open on a refused run too. Somebody who knows their four objections may simply
  type them; that is a better outcome than sending them to find a document containing them. The
  edited structure is **re-inspected**, so the threshold is answerable but not waivable — and only
  the deterministic half runs, because the human has just overruled the material with knowledge the
  material did not contain, and buying a second opinion on their own typing would be expensive and
  rude.
- `POST …/approve` re-inspects as well. Between an edit and an approval there is a network and a
  stale second tab; the run is moved to `insufficient` **before** the 409 is returned, so a polling
  screen and the caller who pressed the button see the same list.

### What the customer actually reads

A list, not a paragraph — the 40.20 screen has to show items that can be ticked off, and usually only
one of them is actionable today. Each item is `{code, message}` from a **closed** vocabulary of
seven: `off_topic`, `too_short`, `no_product`, `no_icp`, `no_objections`, `no_script`, `no_examples`.
The sentences live in `ContentSufficiencyCodes` and every one names a concrete artefact the customer
already has somewhere — a deck, a script, a call recording — because «добавьте больше информации» is
a refusal that teaches nothing.

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

## 6a. Batch adaptation and content review (Phase 40.32)

The pipeline above builds a lesson out of a document. 40.32 is the other direction: take content that
already exists — the base library, a lesson generated here and un-archived, one the РОП wrote by hand
— and either **rewrite it into the customer's voice** or **say what is methodically wrong with it**.

Both are the same machine, distinguished by `ContentAdaptationJobs.Mode`:

| | `tone_rewrite` | `quality_review` |
|---|---|---|
| Question | «Перепиши под наш продукт и тон» | «Что не так с этим упражнением» |
| Item carries | a proposed body + the model's sentence about what changed | a list of codes from a closed vocabulary of seven |
| Can be applied | **yes**, one item at a time, by a person | **no** — a finding is a diagnosis, not a patch |
| ai-service route | `POST /ai/content/rewrite` | `POST /ai/content/review` |

**A batch is a stage.** `POST /admin/content/adaptations {mode, stageKey}` collects every exercise of
that `Skill.Stage` **through 40.18's read resolution** — the organization's own copy where they have
forked a lesson, the global library row where they have not, never both — writes one
`ContentAdaptationItems` row per exercise, and returns immediately. Nothing has been spent yet: the
scope is one database query.

**One LLM call per exercise, and the item is the idempotency key.** 40.27 could put the lease and the
"already paid for" fact on the same row, because a run makes two calls. A batch makes up to sixty, so
they separate: the batch carries the lease, the item carries the answer, and an item carrying an
answer is never queued again. A batch interrupted at item forty resumes at forty-one and the
interruption costs exactly the one call that was in flight. Each item is committed on its own, and the
attempt budget is per item — one exercise the model chokes on must not exhaust a budget protecting
fifty-nine good proposals.

**The queue is the product, and it is walked one item at a time.** `GET
…/adaptations/{jobId}/items/{itemId}` returns three things and merges none of them: the body as it
stands, the body as proposed, and the list of JSON leaves that differ. The model's own sentence
travels beside them, because a leaf list can say `options[1].text` moved and can never say why.
40.18's refusal to three-way-merge prose and grading criteria holds here one level down: nothing on
the server produces a third document.

**Accepting is the only thing in the block that writes an `Exercise`, and it is an HTTP request.**
`ContentAdaptationSweepService` — the ninth entry in
[BACKGROUND_JOBS.md §2.1](TENANCY/BACKGROUND_JOBS.md) — writes proposals and nothing else. There is no
bulk verb: «применить всё» is auto-apply with a person's name attached.

Two guards sit on the accept path:

- **Staleness.** The item carries the SHA-256 of the body the model was shown, and accept recomputes
  it against the row about to be written. A mismatch means somebody edited that exercise after the
  model read it, and the answer is a 409 and a re-run — never a merge.
- **Copy-on-write.** Accepting a rewrite of a **global** exercise forks the lesson first, exactly as
  pressing "edit" would (40.18), and writes the body into the organization's own copy. This is not
  politeness: RLS cannot protect the global library, because "global" is a null and the content policy
  admits `OrganizationId IS NULL` in its `WITH CHECK` clause. Writing the base row would apply one
  customer's tone edit to every other customer's curriculum.

**No version is published.** Accepting writes the draft row, exactly as `PUT /admin/exercises/{id}`
does; minting a `LessonVersion` per accepted sentence would produce a history nobody can read.
Publishing stays the separate human act on the 40.15 route — which also means **the change reaches
learners only when somebody publishes**, and that is the intended shape.

**A rewrite that changed nothing resolves as `unchanged` and never reaches the queue**, and the prompt
tells the model twice that «ничего не меняю» is a permitted answer. The same is true of a review that
found nothing, which is the expected answer. Both exist for the same reason: sixty cosmetic diffs is
how a person learns to accept everything without reading it.

---

## 7. What these blocks deliberately did not do

- **No screen.** Same as 40.15–40.27: the РОП's admin panel is 40.20 and waits on the owner's design.
  See [ADMIN_PANEL.md](ADMIN_PANEL.md). The refusal is machine-readable specifically so that screen
  can render bullets rather than a paragraph when it arrives.
- **No quality judgement on what was generated.** The threshold is about the *input*. Whether the
  four exercises that came out are any good is 40.32.
- **No second LLM call anywhere in 40.28.** The verdict rides the structuring completion; the free
  stage is arithmetic and a word list. The block adds no cost per run and removes cost on the runs it
  refuses before structuring.
- ~~**No promotion into the organization profile.** 40.29.~~ **Done in 40.29, and not here.** The
  promotion is `POST /organizations/profile/draft/apply` in **organization-service**, which takes the
  reviewed structure in its request body from a client that has just read it off
  `GET /admin/content-generation/{jobId}`. Nothing in this pipeline changed: no state, no flag, no
  «promoted» timestamp on the run. A run is disposable and a profile is not, and a run that recorded
  what another service did with its output would be claiming an authority over that row it does not
  have. The one thing worth knowing from this side is that **a run refused after structuring is still
  a usable profile source** — the structure is on the row, and a profile needs less than a lesson does.
  See [ORGANIZATION_SERVICE.md](ORGANIZATION_SERVICE.md#the-profile-as-an-interview-phase-4029).
- **No file upload and no call recordings.** The material is pasted text — or, since 40.31, text
  composed by the server from a measured gap and the organization profile (§3a). 40.30 owns
  recordings, and the consent and retention question it has to answer first.
- ~~**No per-item accept/reject of generated exercises.** 40.32.~~ **Done in 40.32, and as a general
  mechanism rather than a gate bolted onto generation** (§6a). A generated lesson still arrives
  archived, and un-archiving it is still `PUT /admin/lessons/{id}`; what 40.32 adds is that any stage
  of any content — generated, base, or hand-written — can be sent through a proposal queue a person
  answers item by item. The gate the roadmap asked for turned out to be the same gate the tone rewrite
  needed, so there is one.
- **No tests.** Rule №3 in [DONT_FORGET.md](DONT_FORGET.md) — what is missing and why it matters is
  listed there under «Тесты, которых нет».
