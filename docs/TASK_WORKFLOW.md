# Task Workflow — board-driven PLAN → STOP → EXECUTE → VERIFY

Single source of truth for how a card from the **Tasks board** (Nimbalyst tracker)
is turned into shipped code. Both entry points reference *this* file:

- **Manual:** `/run-task <task-id>` — runs with a STOP gate (you approve the plan).
- **Auto:** the polling automation `run-tasks-poll` — runs the same pipeline,
  gated by plan status (see below).

---

## The two items: task vs plan

The tracker has **no native item→item link** (no parent field). We use two items
linked **by a tag**, each with a clear job:

| Item | Holds | Who writes it | Status lifecycle |
|------|-------|---------------|------------------|
| **task** (`tsk_…`) | the **what / why** — the requirement, in your words | **you** | `to-do` → `in-progress` → `done` |
| **plan** (`pln_…`) | the **how** — architecture, API/DB deltas, subtask decomposition | the **PLAN stage (Opus)**; you may seed hints | `draft` → `ready-for-development` → `in-development` → `in-review` → `completed` |

**Linking convention:** the plan carries a tag `task:<task-id>` and its body starts
with `> Task: <task-id> — <title>`. That tag is the only link — `/run-task <task-id>`
resolves the plan via `tracker_list --typeTag plan` + that tag (and creates the plan
if none exists). `planId` (kebab slug) and `planType` (feature/bug-fix/…) are
required fields on the plan.

**The plan status IS the STOP gate** — no chat handshake needed:
`draft` = agent still drafting · **`ready-for-development` = approved → EXECUTE may run**
· `in-development` = executing · `in-review` = verifying · `completed` = shipped.

### So what do you put where?
- **Into the task card:** the requirement. (Your `User Avatars` card is the model.)
- **Into the plan:** nothing by hand is required — the agent fills it. Optionally
  seed *acceptance criteria / constraints / hints* under the "Seed from you" heading
  to steer the planner (e.g. "S3 = local MinIO", "one table vs two — decide & justify").

---

## Models & agents (OMC)

**Default model = Opus, everywhere — except writing tests, which uses Sonnet.**
**Effort = `medium` for every agent/stage.**

| Stage | Agent (`oh-my-claudecode:`) | Model | Effort |
|-------|------------------------------|-------|--------|
| PLAN | `planner` (or `/plan`) | **opus** | medium |
| EXECUTE | `executor` (one per subtask, independents in parallel) | **opus** | medium |
| WRITE TESTS | `test-engineer` | **sonnet** | medium |
| VERIFY | `verifier` (runs suites) + `code-reviewer` (style) | **opus** | medium |
| (debug if VERIFY red) | `debugger` | **opus** | medium |

Independent subtasks are launched **in parallel** — multiple `Agent` calls in one message.

---

## Pipeline

### 0. Pick up the card
- `tracker_get` the task card; resolve its plan by tag `task:<task-id>`
  (`tracker_list --typeTag plan`), reading both in full. If no plan exists, create
  one: type `plan`, title `Plan: <task title>`, tag `task:<task-id>`,
  `fields.planId = <slug>`, `fields.planType = feature|bug-fix|…`.
- Read the docs the card touches: `docs/ARCHITECTURE.md`, `docs/API_CONTRACTS.md`,
  plus any feature doc it references.
- `tracker_update` the task → `status: in-progress`.

### 1. Branch (ONE branch for the whole task)
```
git checkout main && git pull --ff-only
git checkout -b task/<short-id>-<kebab-slug>      # short-id = last 6 of task id
```
**Exactly one branch per task.** Every executor commits to this same branch. Do **not**
create a branch or a git worktree per subtask, and do **not** pass `isolation: worktree`
to executors — parallel subtasks share the one task branch and the one working tree.

### 2. PLAN (Opus) — write into the plan item
Delegate to `oh-my-claudecode:planner` (opus). Write the result **into the plan
item body** (keep status `draft`): affected architecture & API/DB deltas; the
decomposition into **atomic subtasks** (1 subtask = 1 independently testable
commit); and the parallel-vs-sequential ordering.

**Always end the plan with a `## PM Summary` section** — the LAST section, after
all the technical content. 5–8 sentences, in **Russian** (the PM's language),
**non-technical**: what the user gets and why it matters, the visible scope, any
product trade-offs/decisions made, and risks or things the PM should be aware of.
No jargon, file paths, or API names — written for a product manager, not an engineer.
This section is mandatory in **every** plan (manual and auto).

### 3. STOP — approve via plan status
- **Manual (`/run-task`):** present the drafted plan and wait for approval. Approval =
  the plan moves to **`ready-for-development`** (you flip it on the board, or say
  "поехали" and the agent flips it). No code before that.
- **Auto (`run-tasks-poll`):** see "Auto entry behavior" below.

### 4. EXECUTE (Opus, parallel) — code only, **no test runs here**
Set plan → `in-development`. One `oh-my-claudecode:executor` (opus, effort medium) per
subtask; independents concurrent, real dependencies serialized. All commit to the one
task branch (step 1).

**Every executor MUST, before writing code:** read `docs/CODESTYLE.md` and open the
nearest existing sibling file in the same folder, and mirror its conventions. Before
committing a subtask, self-check the diff against CODESTYLE (no abbreviations, **no
comments at all**, no magic strings, one-class-one-file, interface per service).

After each subtask: commit on the task branch (`feat:`/`fix:`/… English, per
`.claude/CLAUDE.md` Rule #4). **Do NOT run the test suite per subtask** — and do not
write tests yet. Tests are written once and run once, in step 5, right before merge.
This is the main speed win: no per-subtask test cycles.

### 5. VERIFY — once, right before merge to main (do not merge until green)
Set plan → `in-review`. Run in order, loop until ALL pass:
1. **Write tests — `oh-my-claudecode:test-engineer` (sonnet, effort medium).** Keep them
   **deliberately sparse — about 2.5× fewer than an exhaustive suite.** Cover only the
   highest-value paths: one happy path per feature area plus the 1–2 riskiest edge cases.
   No tests for trivial getters, DTOs, wiring, or throwaway code. Quality over count.
2. **Run the full suite ONCE** (backend + frontend) — this is the only place tests
   execute in the whole pipeline.
3. **Build / lint:** `dotnet build src/backend/api/Sellevate.Api.csproj`; frontend
   `npm run lint` + `npm run typecheck` — introduce no new errors vs the base branch.
4. **Style review:** `oh-my-claudecode:code-reviewer` (opus) checks the branch diff
   against `docs/CODESTYLE.md` — abbreviations, comments, magic strings, one-class-one-
   file, interface-per-service. Any ❌ blocks merge, exactly like a failing test.

If anything is red → `oh-my-claudecode:debugger` (opus) → fix → re-run. **Loop until
green.** Never merge with failing tests or style violations.

### 6. Docs (Rule #1.4 / Rule #2)
Update what the change touched: `ARCHITECTURE.md`, `API_CONTRACTS.md`, `DB_SCHEMA.md`,
`docs/TESTING/…`; link any new doc in `docs/FEATURES.md`.

### 7. Close out & merge
- Final commit (working state + updated docs); `git push -u origin task/<short-id>-<slug>`
  (push allowed; **never** force-push).
- **Merge to main only after step 5 is fully green** — the pre-merge VERIFY is the single
  test/style gate for the whole task.
- `tracker_update` task → `done`; plan → `completed`.
- `tracker_add_comment` on the task: branch name + one-line summary + test result.

---

## Auto entry behavior (`run-tasks-poll`)
Default (safe): the automation only **EXECUTEs plans already in
`ready-for-development`** — i.e. it drafts/refreshes plans for new `to-do` tasks and
waits for *you* to flip approved ones to Ready on the board. This keeps a human gate
while execution is hands-off. To go **fully unattended**, allow it to self-approve
(let it move its own draft straight to `ready-for-development`) — documented inline in
the automation file.

## Stuck rule
If a subtask fails twice: leave the task `in-progress`, set the plan `blocked`, add a
`tracker_add_comment` with the one-line blocker, and stop — don't thrash. (Mirrors
`.claude/AGENTS.md`.)
