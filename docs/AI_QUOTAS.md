# AI_QUOTAS.md — per-organization voice and LLM limits

> Phase 40.33 of [Phase 40 — Multi-tenancy](ROADMAP.md). The roadmap asks for four things: limits on
> voice minutes and LLM spend **per organization**; enforcement **on ai-service**, the single point
> every voice and LLM call passes through, and not reinvented in each service; a customer running
> voice for a day degrading **only their own organization**; and spend visible in a dashboard
> **before** it is visible on the provider's invoice.
>
> Companion docs: [AI_SERVICE.md](AI_SERVICE.md) (the service), [VOICE_ROLEPLAY.md](VOICE_ROLEPLAY.md)
> (the voice feature), [MONITORING.md](MONITORING.md) (the platform-wide metrics),
> [LLM_FAILURE_HANDLING.md](LLM_FAILURE_HANDLING.md) (what a refusal is *not*).

---

## 1. The single point, and the two doors that were beside it

The whole design rests on one sentence: **every call to a paid LLM or speech provider is made by
ai-service**. Before this block that sentence was false in two places, and both were invisible from
the code that claimed it:

- **`learning-service/Infrastructure/Ai/OpenAiChatService.cs`** — a 362-line monolith-era copy of
  ai-service's chat client, used by `ExerciseDialogService` for the interactive `ai_dialogue`
  exercise. `docs/DONT_FORGET.md` has carried it since 40.27, which deliberately did not extend it
  and left its removal to this block.
- **`learning-service/Infrastructure/Ai/YandexTtsService.cs` + `TtsRouter.cs`** — a second copy of
  the speech client, holding its own `YandexTts:ApiKey`. This was the sharper hole: voice minutes had
  been metered per organization in ai-service since 40.11, and *every sentence an exercise spoke* was
  synthesized outside that meter.

Both are gone. learning-service now calls three new internal ai-service routes and holds no provider
key of any kind — `OPENAI_API_KEY` and `YANDEX_TTS_API_KEY` were removed from its compose block and
from `scripts/lib-local-env.sh`.

**The claim is now checked, not remembered:** `scripts/ai-provider-lint.py` fails when any file
outside a six-entry allow-list opens a provider-named `HttpClient` or names a provider host. Run it
with the tenancy lints. It would have caught both deleted copies.

```
python3 scripts/ai-provider-lint.py
```

### The four metered call sites

| File | What it calls | Gate | Charge |
|---|---|---|---|
| `Features/Dialog/…/OpenAiChatService.cs` | every non-streaming completion in the product | before the request is built | from the provider's `usage` block |
| `Features/Dialog/…/OpenAiChatService.cs` (streaming) | dialog and exercise voice turns | before the request is built | **estimated** from characters, counted separately |
| `Features/Evaluation/…/AiEvaluationStrategyBase.cs` | the five AI grading strategies | before the POST | from `usage`, else estimated |
| `Features/Voice/…/YandexTtsService.cs` | speech synthesis | (voice minutes gate, §4) | characters actually synthesized |
| `Features/Transcription/…/WhisperTranscriptionService.cs` | Whisper STT | **none** — see §7 | transcribed characters |

`AiEvaluationStrategyBase` was **not** folded into `IOpenAiChatService` even though the two now do
almost the same thing. It has its own failure contract — it throws `HttpRequestException` and
degrades an unparseable answer into a failed-but-valid result rather than a 503 — which
`LLM_FAILURE_HANDLING.md` pins and tests assert. Merging them would have been a behaviour change
smuggled in under a metering change. What *is* shared is `OpenAiUsageReader`, so the two paths cannot
count tokens differently.

---

## 2. Where the limits live, and what an absent row means

`OrganizationQuotas`, a table in **ai-db** — the database of the service that enforces it.

The obvious alternative was a column set on organization-service's `OrganizationProfile`, replicated
here over `organization.profile.updated` exactly as 40.19's `OrganizationProfileReplicas` are. It was
rejected on one concrete failure: **the moment an operator most needs to change a limit is the moment
a customer is standing at it.** A Kafka replica that is lagging — or whose consumer is dead-lettering
— would leave the raise invisible to the enforcer, with nothing in the raise's own response saying
so. The row the meter reads is the row the operator wrote.

Every column is nullable, and **null means "the platform default"** from the `AiQuotas` configuration
section. This is what dissolves the fail-open / fail-closed question:

> An organization with no quota row is **not unmetered**. It is metered against the platform
> defaults — which is exactly what ai-service did before this block, when voice limits came from
> `Voice:DailyLimitMinutes` and applied to everybody.

So there is no window in which a missing row, a fresh tenant, or a failed replication hands anybody a
free night of voice. What a missing row costs is precision, not enforcement.

| Column | Meaning | Null |
|---|---|---|
| `VoiceDailyLimitMinutes` | organization-wide voice minutes per UTC day | `AiQuotas:DefaultVoiceDailyLimitMinutes` (600) |
| `VoiceMonthlyLimitMinutes` | organization-wide voice minutes per UTC month | `…DefaultVoiceMonthlyLimitMinutes` (6000) |
| `LlmMonthlyTokenLimit` | prompt + completion tokens per UTC month, all models | `…DefaultLlmMonthlyTokenLimit` (20 000 000) |
| `BatchReservePercent` | share of the LLM allowance batch work may not touch | `…DefaultBatchReservePercent` (10) |
| `Note` | free text for the operator: which contract this number came from | — |

Null and zero mean different things on purpose, but **not the two things this document used to
claim**. It said `0` "disables that window explicitly", which reads as *closes* it. The code says the
opposite: every gate short-circuits on a non-positive limit —
`AiSpendMeter.EnsureLlmBudgetAsync` and `HasLlmBudgetAsync` both `return` early when
`LlmMonthlyTokenLimit <= 0`, the Lua reserve script guards on `limit > 0`, and
`AiQuotaService.DescribeState` reports no state for one. So:

| Value | What actually happens |
|-------|-----------------------|
| `null` | the platform default applies (`AiQuotas:Default*`) |
| `0` (or negative) | **the ceiling is removed — that window is unmetered** |

Corrected 2026-08-19, when the platform quota screen was built against the code rather than against
this page. The screen warns about it inline, because an operator typing `0` to shut a customer off
would achieve exactly the reverse. If «closed» is a state the product needs, it needs a
representation of its own — `0` is already taken.

---

## 3. What is limited: tokens, not calls and not money

Three candidates, and the choice is not cosmetic.

- **Calls** are trivial to count and wildly inaccurate: a fifty-page structuring call and a
  three-word dialog turn would count the same, and the whole point of the limit is that they do not.
- **Money** is what the customer's contract is written in, but our number for it is a price table we
  maintain by hand. A limit denominated in money silently moves for every customer the day somebody
  edits a constant.
- **Tokens** are what the provider actually bills and what we can count exactly. They are the unit.

So: **the limit is counted in tokens; money is derived for display only.** Editing
`AiQuotas:PricePerMillionTokens` re-renders history and moves no limit. Usage is stored **per model**
(`AiUsageRecords` is keyed by organization + month + model) because models differ in price by more
than an order of magnitude and a blended token total cannot be turned into money without lying.

### Exact where it matters, estimated where it cannot be

| Path | Accounting |
|---|---|
| Every non-streaming completion — structuring, generation, rewrite, review, feedback, personas, briefings, grading | **Measured**, from the provider's `usage` block |
| Streamed dialog turns (`/dialog/sessions/{id}/voice/stream`, exercise voice) | **Estimated**, characters ÷ 4 |

An SSE stream carries no `usage`, and asking for one via `stream_options` is a request shape not every
OpenAI-compatible gateway accepts — breaking every voice call in order to make the cheapest call in
the product exact is a bad trade. The expensive calls are all non-streaming and all measured; a
streamed turn is capped at `OpenAI:MaximumDialogTokenCount` (500) anyway. Estimated calls are counted
in their own column (`EstimatedCallCount`) and their own metric label, so nobody reads an estimate as
a measurement.

> A call recorded as **zero** tokens would be worse than one recorded approximately: zero reads as
> "this model is free" on the spend report, and the month would quietly understate itself.

---

## 4. Voice: two windows, and why the per-user one stays

Voice has been metered per user since the feature shipped — `Voice:DailyLimitMinutes` (30) and
`MonthlyLimitMinutes` (300), enforced by `VoiceUsageService` with a Redis reserve/refund and a Lua
check-and-increment. 40.11 gave those keys an `org:{orgId}:` prefix and noted that per-organization
limits were coming.

40.33 adds an **organization-wide** window under the per-user one. Both apply, and both are kept
deliberately: the per-user limit stops one person burning the whole organization's day, which the
organization limit alone cannot; the organization limit stops the thing the roadmap names — *«один
клиент, гоняющий голос сутками»* — which the per-user limit alone could not, because a customer's
total spend used to be however many users they had times the per-user allowance.

```
org:{orgId}:voice:{userId}:day:{y}:{m}:{d}     ← per user (since the voice feature)
org:{orgId}:voice:{userId}:month:{y}:{m}
org:{orgId}:voice:org:day:{y}:{m}:{d}          ← per organization (40.33)
org:{orgId}:voice:org:month:{y}:{m}
```

`voice:org:` cannot collide with `voice:{userId}:` — a user id is a GUID and never the literal `org`.

The organization reservation happens **after** the two per-user ones, and a refusal rolls both of
those back before it throws, so a blocked call leaves no phantom reservation behind. The caller sees
the same `429` shape it always has; the `period` field names `organization day` / `organization
month` instead of `daily` / `monthly`.

Durable voice accounting is unchanged: actual seconds go to Mongo `dialog_sessions` in the refund
step, and `GET /admin/voice/usage` still aggregates them per user. That endpoint grew four fields for
the organization window (`organizationDailyLimitSeconds`, `organizationMonthlyLimitSeconds`,
`organizationUsedSecondsToday`, `organizationUsedSecondsThisMonth`), because the per-user rows answer
«кто много говорит» and never answered «сколько осталось у компании».

---

## 5. Soft or hard: what actually degrades

The roadmap says a customer running voice all day must degrade **only their own organization**. That
is a statement about isolation, not about mercy — but stopping everything at once is not the only
reading, and it is not the one this block implements.

**Two ceilings, one budget.**

| Workload | Ceiling | Declared by |
|---|---|---|
| **Interactive** — a dialog turn, a graded exercise, an admin pressing a button | 100% of the monthly token limit | default; `X-Ai-Workload: interactive` |
| **Batch** — content generation (40.27), batch tone adaptation and AI review (40.32) | 100% − `BatchReservePercent` (default 90%) | `X-Ai-Workload: batch` |

So an organization that has burned its month loses its **overnight content pipeline before it loses
the conversation a rep is in the middle of**. That is the degradation the roadmap's sentence is
about, applied inside one organization as well as between them.

ai-service cannot infer the class — an internal POST looks identical either way — so the caller
declares it in a header. **Absent means interactive**, the class with the *larger* allowance: a caller
that has never heard of the header gets the permissive answer rather than being quietly held at 90%.

Four states appear in the spend report's `quotaState`:

| State | Meaning |
|---|---|
| `ok` | below the soft threshold |
| `warning` | past `AiQuotas:SoftWarningPercent` (80%). Refuses nothing; it exists so somebody sees the wall coming |
| `batch_paused` | past the batch ceiling. Pipelines have stopped; conversations have not |
| `exhausted` | at the limit. Interactive calls answer 429 too |

A refusal is `429` with `{resource, period, used, limit}` — not `402`, which is reserved for the
*provider* telling us **our** balance is empty (`OpenAiPaymentRequiredException`). Conflating the two
would make a customer's cap look like our outage.

---

## 6. The background pipelines: checked before the lease, charged once

40.27 and 40.32's sweeps claim work with **one conditional `UPDATE` that also spends an attempt**,
and only then call. Discovering the quota wall after that point costs an attempt and holds a lease
for an organization that was never going to be served; three ticks of that and the run is `failed`
for a reason that has nothing to do with the run.

So each sweep asks first, once per organization per tick, before the step runner is resolved:

```
GET /ai/quota/preflight?workload=batch   →  {"allowed": true|false}
```

**The preflight only reads.** The charge happens exactly once, in the meter, when a completion comes
back — so calling the preflight twice changes nothing and there is no double counting with the
pipelines' own 60-item ceiling.

**It fails open, and only it.** If the preflight cannot be answered — network blip, ai-service
restarting — the sweep proceeds and lets the real gate decide, because the preflight is an
optimisation and the gate it optimises is on the other side and cannot be skipped. Treating an
unreachable preflight as "no allowance" would let one flapping connection stop every customer's
content pipeline while their budgets sat untouched.

---

## 7. Deliberate gaps

- **STT is recorded but not gated.** Whisper bills per second of audio; `POST /transcription/transcribe`
  forwards a file it never decodes and so never learns the duration. Transcribed characters are a
  proxy good enough to see a spike on the report and not good enough to refuse a call on.
- **An unattributed metered call is refused, not counted to nobody.** All six internal callers
  (learning ×3, company ×4 minus overlap) forward `X-Organization-Id` as of this block, and ai-service
  answers `400` when one does not — a caller mistake with a fixed remedy, reported as such rather than
  as a server fault. Platform staff and system-mode work pass through ungated: they have no
  organization and no budget, and refusing them would break the admin screens.
- **A lost meter write never fails the call.** The provider has already been paid by the time the
  ledger is written; throwing there would lose the answer the customer paid for as well as the record
  of it. `AddToLedgerAsync` logs a warning and returns.
- **Overshoot is bounded, not zero.** The price of a completion is unknown until it exists, so the
  gate reads before and the charge adds after. Concurrent calls that all pass at 99% can each overshoot
  by one call. Reserving a worst-case token budget per call and refunding it would refuse work an
  organization could afford, every time, in exchange for a bound nobody needs.

---

## 8. Endpoints

| Method | Path | Who | What |
|---|---|---|---|
| `GET` | `/admin/ai-usage` | organization administrator, or platform staff | This month's spend: per-model tokens, calls, speech characters, derived cost, `quotaState`, and the voice windows |
| `GET` | `/admin/ai-quota` | platform staff only | The organization's allowance and the effective numbers behind it |
| `PUT` | `/admin/ai-quota` | platform staff only | Set it. Omitted fields clear to the platform default |
| `GET` | `/ai/quota/preflight?workload=` | internal (shared secret) | Whether a sweep should claim work |
| `POST` | `/ai/chat`, `/ai/chat/stream`, `/ai/tts` | internal (shared secret) | The generic completion and synthesis learning-service used to do itself |

`/admin/ai-usage` is **organization-scoped and readable by the РОП**, because the person who needs to
know their content pipeline is about to stop is the one whose pipeline it is. `/admin/ai-quota` is
platform-only: a quota is what the customer bought, and an organization administrator raising their
own is not an administrative action, it is a purchase.

Neither carries an organization id in the route or the body — the tenant is the caller's
`X-Organization-Id`, which for platform staff is the organization they impersonated into (40.9).
`scripts/tenancy-boundary-lint.py` enforces that shape.

**Both are proxied** (`ai-admin-ai-usage`, `ai-admin-ai-quota`, plus their catch-all siblings in
`gateway/appsettings.json`). 40.32 found that `/admin/content/*` had been unreachable since 40.18
because nobody added the route; this block checked before shipping.

A platform administrator with **no** organization header reads the installation-wide total, because
`AiUsageRecords` follows the codebase's `IsPlatformWide` widening (40.16). That is the one
cross-organization total in this service, and it is safe in a way `/admin/voice/usage` was not: it
returns per-model token counts and no identities at all.

---

## 9. Where the numbers live

| Store | Holds | Why there |
|---|---|---|
| Postgres `ai` — `OrganizationQuotas` | the allowance | read by the enforcer, written by the operator, no replication in between |
| Postgres `ai` — `AiUsageRecords` | the month's spend, per model | durable and exact; the report has to be readable next month, and a Redis eviction must not silently zero it. One `INSERT … ON CONFLICT DO UPDATE SET x = x + excluded.x` per call — atomic, no read-modify-write |
| Redis | the voice reserve/refund counters | sub-second, many per minute, and voice already has its durable record in Mongo `dialog_sessions` |
| Prometheus | platform-wide totals | see [MONITORING.md](MONITORING.md) — **never** per organization |

Both Postgres tables are strict tenant data: RLS with plain equality, the tenant column leading the
primary key, the same call `OrganizationProfileReplicas` made in 40.19. There is no global allowance
and no global bill.

**No long index, therefore no `docs/TENANCY/sql/40.33_*_indexes_concurrently.sql`** — a decision, not
an omission. Every read is a prefix scan on the leading key columns of a table holding one row per
organization per model per month.
