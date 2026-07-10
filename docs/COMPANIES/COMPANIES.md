# Companies (Компании)

**Status:** Stage A shipped (Phase 39.1–39.7). Stage B contacts mini-CRM (39.9), status
pipeline (39.10), follow-up reminders (39.11), AI pre-call briefing (39.12), and AI real-call-log
parsing (39.13) are shipped. The rest of Stage B (AI persona generation, voice memo, readiness
score) is **planned, not implemented** — see `docs/ROADMAP.md` Phase 39.14+.

Design reference: [docs/COMPANIES/DESIGN_SPEC.md](DESIGN_SPEC.md) (screen layout, copy, CSS).
API reference: [docs/API_CONTRACTS.md](../API_CONTRACTS.md) (`Companies` section + `POST /dialog/sessions`
company-context addendum). Schema reference: [docs/DB_SCHEMA.md](../DB_SCHEMA.md) (`company` database).

## What it is

A private CRM-lite for the salesperson: keep a list of real prospect companies, write a
free-form description of each one, practice a cold call against that description with an AI
playing the company's side (voice or chat), and log real calls that happened (who / what about
/ outcome). Practice sessions and real-call log entries share one chronological timeline per
company.

## Architecture

### `company-service` (new microservice, port 5009)

`src/backend/company-service/Company`, scaffolded after `notification-service`'s pattern
(Serilog → Loki, per-service JWT bearer validation, CORS, health checks, ProblemDetails). Owns a
standalone Postgres database `company`, auto-migrated on startup
(`DatabaseBootstrapper` / EF Core migrations). **Kafka producer only** (since Phase 39.11) — it
publishes `company.followup.due` via a polling background service but consumes nothing; see
"Follow-up reminders" below and `docs/ARCHITECTURE.md` for the registration/trade-off details.

Routed at the gateway via YARP cluster `company` (`company-companies` / `company-companies-root`
routes in `src/backend/gateway/Gateway/appsettings.json`), `docker-compose.yml` service entry, and
`scripts/dev-company.sh` (`LOCAL_COMPANY_PORT=5009`) wired into `scripts/dev-up.sh`.

Every controller action resolves `UserId` from the JWT (`ClaimTypes.NameIdentifier`) and every
query/mutation is scoped `WHERE UserId = ...` (or via an ownership existence check for
sub-resources). An id that exists but belongs to another user is indistinguishable from an id
that doesn't exist — both return `404`, never `403`.

### ai-service — company-context sessions

Practice calls are ordinary `DialogSession`s (same Mongo document, same voice/chat pipeline,
feedback, XP, and minute quotas as any other dialog mode) with two additions:

- **Seeded hidden mode.** `CompanyCallModeSeeder` (`src/backend/ai-service/Ai/Features/Dialog/Seeders/CompanyCallModeSeeder.cs`)
  creates one `DialogBundle` (`IsHidden: true`, so it never appears in `GET /dialog/bundles`) and
  one `DialogMode` with key `company-call`, `VoiceEnabled: true`, and admin-editable chat/feedback
  system prompt templates ("play a company employee/decision-maker taking a cold call...",
  "score against the user's stated call goal..."). Both are regular admin-editable rows — a
  superadmin can tweak the prompts at `/admin/dialog` like any other mode.
  `GET /dialog/company-call-mode` exposes the fixed `{bundleId, modeId}` pair (404 if the seeder
  hasn't run yet); the frontend fetches this once (`useCompanyCallMode`) before letting the user
  start a call.
- **Prompt context injection.** `POST /dialog/sessions` accepts an optional
  `companyContext: {companyName, companyDescription, callGoal?}` (`StartSessionRequestDto`).
  When present, `companyContext` is only accepted alongside the seeded `company-call` mode — any
  other mode + context combination is `400`. `CompanyContextPromptBuilder`
  (`src/backend/ai-service/Ai/Features/Dialog/Helpers/CompanyContextPromptBuilder.cs`) appends a
  small `---\nКомпания: ...\nОписание: ...\nЦель звонка пользователя: ...` block to both the chat
  and feedback system prompts at session-start time. The context is **not** persisted in
  PostgreSQL — it's folded into the prompt text and separately stored verbatim on the Mongo
  `DialogSession` document (`companyCallContext`) purely for the record; nothing downstream
  (voice stream, feedback generation, XP weighting, minute quotas) changes — they all operate on
  the composed prompt exactly as with any other mode.
- **Validation asymmetry (500 vs 1000).** `CompanyCallContextDto.CallGoal` on the ai-service side
  caps at **500 chars** — this is what actually reaches the AI prompt. `company-service`'s
  `PracticeCall.Goal` (and the `CreatePracticeCallRequestDto`) caps at **1000 chars** — this is
  the full goal as the user typed it, stored for the "recent goals" feature. The frontend
  enforces the 500-char cut when building `companyContext` for the dialog session
  (`COMPANY_CONTEXT_GOAL_MAX = 500` in both call pages) while sending the untruncated goal to
  `POST /companies/{id}/practice-calls`.

## Data model (company-service, Postgres `company`)

Three entities, all owned by `UserId`, cascade-deleted with their parent `Company`:

- **`Company`** — `Id, UserId, Name (≤200), Description (≤8000, default ""),
  Status (Lead/Contacted/MeetingScheduled/DealWon/DealLost, default Lead, stored as string),
  NextActionAt (nullable), NextActionNote (nullable, ≤2000), FollowUpNotifiedAt (nullable),
  CreatedAt, UpdatedAt`. Index on `UserId`; sparse index on `NextActionAt`. Status is changed via
  a dedicated `PUT /companies/{id}/status` endpoint (kept separate from the name/description
  update so the frontend can fire a status change without re-submitting the whole edit form); the
  follow-up trio is likewise changed via its own `PUT /companies/{id}/follow-up` endpoint — see
  "Follow-up reminders" below.
- **`CallLogEntry`** (a real call the user logs manually) —
  `Id, CompanyId, UserId, ContactName (≤200), Subject (≤4000), Outcome (≤4000), OccurredAt,
  CreatedAt, UpdatedAt`. Only `ContactName` (with `Subject`/`Outcome` empty-allowed) plus
  `OccurredAt` are required by the DTO — matches the "only «С кем» is mandatory" product rule.
  Index `(CompanyId, OccurredAt DESC)`.
- **`PracticeCall`** (a link row created when an AI practice session starts) —
  `Id, CompanyId, UserId, DialogSessionId (≤100, the ai-service Mongo session id, stored as
  string), Goal (≤1000), CreatedAt`. Index `(CompanyId, CreatedAt DESC)`. There is no foreign key
  into ai-service's Mongo store — `DialogSessionId` is an opaque reference the frontend uses to
  fetch feedback/XP from ai-service directly.

`CompanySummaryDto`/`CompanyDetailDto` also carry a live `callLogCount`/`practiceCallCount`
(computed via `EF` `Count()`, not denormalized columns).

## The goal-handoff contract

The pre-call goal input lives on `/companies/[id]` (`PrecallPanel`), but the actual call happens
on a separate full-screen route (`/companies/[id]/call/voice` or `/call/chat`, outside the
`(main)` layout). The goal has to survive that navigation without a shared React state tree:

1. On "Позвонить"/"Чат" click, the company page writes the trimmed goal to
   `sessionStorage["company-call-goal:{companyId}"]` **and** pushes the URL with
   `?goal=<encoded>` as a fallback (`app/(main)/companies/[id]/page.tsx`, `handleCall`/`handleChat`).
2. The call page's `readStoredGoal(companyId, queryGoal)` reads `sessionStorage` first; the query
   param is only used if `sessionStorage` has no entry for that key (e.g. a fresh tab / direct
   link). **`sessionStorage` is authoritative** — it's what survives an empty-string goal
   correctly (the query param would be `?goal=` either way, but `sessionStorage.getItem` returning
   `""` vs `null` disambiguates "explicitly no goal" from "no data at all").
3. The goal is captured once into local state on mount (`useState(() => readStoredGoal(...))`) —
   it does not change for the lifetime of that call screen even if a background tab edits
   `sessionStorage`.
4. The same key is used by both the voice and chat call pages, so a user who navigates directly
   between `/call/voice` and `/call/chat` for the same company keeps the same goal.

## Frontend structure

- **Routes:**
  - `/(main)/companies` — list (search, create modal, empty/loading/error states)
  - `/(main)/companies/[id]` — company detail (identity header, description card, pre-call panel,
    timeline, log CRUD, edit/delete)
  - `/companies/[id]/call/voice` and `/companies/[id]/call/chat` — full-screen call routes,
    intentionally **outside** the `(main)` layout group (no nav rail/bottom nav/top bar) to match
    the existing full-screen voice-call UX
- **`features/companies/`** (mirrors `features/dialog/`):
  - `hooks/use-companies.ts` — list/get/create/update/delete companies (`useCompanies`,
    `useCompany`, `useCreateCompany`, `useUpdateCompany`, `useUpdateCompanyStatus`,
    `useUpdateCompanyFollowUp`, `useDeleteCompany`), client-side name filter on top of the
    server `?search=`
  - `lib/company-status.ts` — single source of truth for the 5 status values, their Russian
    labels (Лид / Был контакт / Встреча назначена / Сделка закрыта / Отказ), and the CSS tone
    class per status; `components/company-status-badge.tsx` (read-only chip, used on list rows)
    and `components/company-status-menu.tsx` (click-to-open dropdown, used on the company header)
    both key off it — status filter chips on `/companies` and the list-row badge share the same
    tone classes (`co-status--lead|contacted|meeting|won|lost` in `app/globals.css`)
  - `lib/company-followup.ts` — due/overdue tone for a `nextActionAt` (due: within 24h, overdue:
    past); `components/company-followup-badge.tsx` (renders nothing when there's no follow-up or
    it's more than a day out) and `components/company-followup-card.tsx` (date + note editor on
    the company page) both key off it — tone classes `co-followup--due|overdue` in `app/globals.css`
  - `hooks/use-company-logs.ts` — call-log CRUD
  - `hooks/use-practice-calls.ts` — `useCompanyPracticeCalls`, `useRecentGoals`,
    `useCreatePracticeCall` (retries twice; failure toasts but doesn't block the call)
  - `hooks/use-company-call-mode.ts` — fetches the seeded `{bundleId, modeId}` once
  - `hooks/use-company-briefing.ts` — `useCompanyBriefing` (GET, normalizes a 204 to
    `{content: null, generatedAt: null}` since React Query query functions may not return
    `undefined`), `useGenerateCompanyBriefing` (POST, writes the result straight into the query
    cache on success)
  - `components/` — `company-row`, `company-modal` (create/edit), `company-header`,
    `company-description-card`, `precall-panel`, `company-briefing-card`, `company-timeline`
    (+ `timeline-practice-item`, `timeline-reallog-item`), `call-log-form`/`call-log-modal`,
    `confirm-delete-modal`
  - `lib/timeline.ts` — merges `PracticeCall[]` + `CallLogEntry[]` into one sorted, filterable
    feed (`mergeTimeline`, `filterTimeline`); `lib/format.ts`, `lib/avatar.ts` — display helpers
- **Nav:** `briefcase` icon added to `IconName` (`shared/components/icon.tsx`); rail item
  «Компании» in the desktop nav rail; on mobile, «Компании» **replaces** «Справочник» in the
  5-slot bottom nav bar (`features/layout/components/bottom-nav.tsx` `NAV_ITEMS`) — the guidebook
  stays reachable from the desktop rail only, per DESIGN_SPEC §1.4.

## Flows

**Create → describe → practice:**
1. `/companies` → «Добавить компанию» → name (+ optional description) → company page opens.
2. Description is editable inline on the company page (`CompanyDescriptionCard`, edit mode →
   `PUT /companies/{id}`).
3. Pre-call panel: type a goal (or pick one of the last 5 distinct goals used for this company,
   `GET /companies/{id}/recent-goals`) → «Позвонить» or «Чат».
4. Handoff (see contract above) to `/call/voice` or `/call/chat`. Both pages: fetch the company
   and the seeded call-mode id in parallel, build `companyContext` from the company's
   name/description + the (500-char-capped) goal, and hand it to the existing dialog/voice
   session-start flow unchanged (`useVoice({ ..., companyContext })` for voice,
   `startDialogSession` for chat).
5. On session creation, `POST /companies/{id}/practice-calls {dialogSessionId, goal}` (untruncated
   goal) links the new session to the company — this list write is retried but non-blocking; a
   failure only toasts, the call itself proceeds.
6. Hangup/end → existing `completeDialogSession` → feedback modal (same component as any other
   dialog mode) → closing the modal returns to `/companies/{id}`.
7. The new practice call appears at the top of the combined timeline immediately (query
   invalidation on `practice-calls` and `recent-goals`).

**Real-call logging:** «Записать звонок» on the timeline card opens an inline form (3 fields: с
кем / о чём / к чему пришли + occurred-at date) — only "с кем" is required. Submits
`POST /companies/{id}/logs`; edit/delete via the same form in a modal + a confirm-delete dialog.

**Timeline:** `CompanyTimeline` merges practice calls and real-call log entries into one
reverse-chronological list with a three-way segmented filter (Все / Тренировки / Звонки,
`lib/timeline.ts`); the "add log" affordance is hidden while the "Тренировки"-only filter is
active (there's nothing to log for a filtered-out list).

**Delete company:** confirm modal warns that description, practice history, and the call log are
all deleted together — matches the `OnDelete(DeleteBehavior.Cascade)` FK behavior on both child
tables.

## Edge cases

- **Voice minute quota (429 from ai-service):** the shared `useVoice` hook already surfaces a
  429 as a quota-exceeded error state; the company call pages render it through the same
  `error`/toast path as the generic voice-call screen — no company-specific handling needed.
- **Company-call mode not yet seeded (`GET /dialog/company-call-mode` → 404):** both call pages
  show a dedicated "Тренировочные звонки недоступны" card with a link back to the company instead
  of attempting to start a session.
- **Ownership violations:** any `{id}` (company, log, or the implicit practice-call/company pair)
  that doesn't belong to the caller returns `404` uniformly — the frontend's `CompanyPage` reacts
  to a 404 on `GET /companies/{id}` with "Компания не найдена" and a link back to `/companies`;
  the same pattern is used on the call pages (`isCompanyNotFound` → toast + redirect).
- **AI/network unavailable:** `useCreatePracticeCall` retries twice then toasts
  "Не удалось сохранить тренировку в историю компании" without blocking the call; company CRUD
  mutations toast a generic "Не удалось сохранить/удалить компанию: {message}" on failure.
- **Empty description:** the pre-call panel still allows calling but shows a hint
  ("Совет: заполните описание компании — звонок станет реалистичнее") instead of the "AI will
  play this company" message.

## Company status pipeline (Phase 39.10)

`Company.Status` is a 5-value enum — `Lead` (Лид, neutral) → `Contacted` (Был контакт, info) →
`MeetingScheduled` (Встреча назначена, violet) → `DealWon`/`DealLost` (Сделка закрыта / Отказ,
success/danger) — persisted as a string column, defaulting new (and pre-migration existing) rows
to `Lead`. There's no server-side enforced transition order; any status can be set from any other.

- **Backend:** `PUT /companies/{id}/status` (`{ "status": "Contacted" }`) returns the updated
  `CompanyDetailDto`; `404` on missing company or wrong owner, same ownership pattern as every
  other company endpoint. `status` is included on both `CompanySummaryDto` (list) and
  `CompanyDetailDto` (detail).
- **Frontend:** status chip on each `/companies` row; status filter chips in the list toolbar
  (client-side filter over the already-fetched list, same pattern as the name search — no extra
  network call); a click-to-open status dropdown on the company page header
  (`CompanyStatusMenu`) that calls `useUpdateCompanyStatus` on selection.

## Follow-up reminders (Phase 39.11)

A salesperson schedules a next-contact date (+ optional note) per company; company-service polls
for due dates and notifies via the shared notification inbox — no client-side reminder logic.

- **Backend (company-service):** `PUT /companies/{id}/follow-up` (`{ "nextActionAt": "...",
  "nextActionNote": "..." }`) sets `NextActionAt`/`NextActionNote` and resets
  `FollowUpNotifiedAt` to `null` (so a rescheduled due date is eligible to notify again, even if
  the previous one already fired); passing `nextActionAt: null` clears all three fields — there's
  nothing left to remind about. A hosted `FollowUpReminderBackgroundService` polls every
  `FollowUpReminder:PollIntervalMinutes` (default 5) for `NextActionAt <= now AND
  FollowUpNotifiedAt IS NULL`, claims matching companies (sets `FollowUpNotifiedAt`, commits),
  then publishes one `company.followup.due` Kafka event per claim (payload: `companyId, userId,
  companyName, nextActionAt, note`). See `docs/ARCHITECTURE.md` for why the claim commits
  *before* the publish (an at-most-once trade-off appropriate for a single-instance, non-outbox
  producer) and `docs/API_CONTRACTS.md` for the full contract.
- **notification-service:** consumes `company.followup.due` → `NotificationType.CompanyFollowUpDue`,
  an **in-app-only** notification (no email) titled *«Пора связаться с {companyName}»*, body is
  the follow-up note (or a generic fallback when there's none), `actionUrl` `/companies/{id}`.
  Dedupes on `companyId:nextActionAt` (not just `companyId`) so a reschedule — which resets
  `FollowUpNotifiedAt` on the producer side — still produces a fresh notification instead of
  being suppressed by the still-inboxed reminder for the earlier due date.
- **Frontend:** a "Следующий контакт" card on the company page (date + note editor, "Убрать
  напоминание" to clear) below the description card; a due/overdue badge
  (`CompanyFollowUpBadge`) on `/companies` rows and on the card itself — due (within 24h) uses
  the info tone, overdue uses the danger tone, nothing renders otherwise. Mirrors the 39.10
  status-badge pattern (`lib/company-followup.ts` + `.co-followup-*` CSS tone classes in
  `app/globals.css`, parallel to `.co-status-*`).

## AI pre-call briefing / "Шпаргалка" (Phase 39.12)

A one-click markdown cheat sheet before a call: кто они / о чём договаривались / возможные
возражения / следующий шаг, generated by ai-service from the company's own data and cached on the
company row.

- **ai-service:** stateless internal endpoint `POST /ai/companies/briefing`
  (`src/backend/ai-service/Ai/Features/Companies/BriefingController.cs`, guarded by the same
  `InternalServiceAuthFilter` as the Evaluation feature). `BriefingService` composes a Russian
  system prompt (fixed 4-heading markdown format instructions + the caller-supplied company
  description/goal/recent-calls/feedback-summaries block) and calls
  `IOpenAiChatService.GenerateTextAsync` — a new one-shot plain-text completion method added
  alongside the existing dialogue-shaped `SendChatMessageAsync` (forces a `{reply, endCall}` JSON
  contract) and `GenerateFeedbackAsync` (forces an HTML + `[XP:N]`-tag contract), neither of which
  fits free-form markdown output.
- **company-service:** `POST /companies/{id}/briefing` gathers context — `Company.Description`,
  the single most recent non-empty `PracticeCall.Goal` (not the last-5-distinct list used by
  `/recent-goals`), and the last 5 `CallLogEntry` rows (newest first) — calls ai-service via
  `BriefingAiClient` (mirrors learning-service's `AiEvaluationClient`: typed `HttpClient`,
  `AiService:BaseUrl`/`BriefingPath` config, `X-Internal-Service-Secret` header from
  `InternalAuth:ServiceSecret`), and caches the returned `{content, generatedAt}` on
  `Company.BriefingContent`/`BriefingGeneratedAt`. `GET /companies/{id}/briefing` returns the
  cache without calling ai-service (`204` if never generated). Both endpoints follow the standard
  ownership/`404` pattern; `POST` returns `503` if ai-service is unreachable.
  **Feedback summaries are not wired up** — practice-session feedback text lives in ai-service's
  Mongo store, and company-service has no cross-service read into it (kept out of scope for
  39.12), so `feedbackSummaries` sent to ai-service is always `[]`; the prompt degrades gracefully
  when that section is empty. See `docs/API_CONTRACTS.md` for the full contract.
- **Frontend:** a "Шпаргалка к звонку" card (`components/company-briefing-card.tsx`,
  `hooks/use-company-briefing.ts`) below the follow-up card on the company page — generate/
  regenerate button, `react-markdown` render of the cached content (already a frontend
  dependency, same renderer used by the guidebook/reference pages), a relative "Обновлено ..."
  timestamp, loading/empty/error states. Regenerating overwrites the cache; there's no history of
  past briefings.

## AI real-call log parsing / "Вставить заметки" (Phase 39.13)

Lets a manager paste raw notes or a call transcript instead of typing the three log fields by
hand — ai-service extracts a draft, the user reviews/edits it, then saves through the existing
log-create flow. Persists nothing on its own; it's a pure extraction proxy.

- **ai-service:** stateless internal endpoint `POST /ai/companies/parse-log`
  (`src/backend/ai-service/Ai/Features/Companies/ParseLogController.cs`, same
  `InternalServiceAuthFilter` guard and `MaxRawTextLength` (16000-char) size guard pattern as
  `BriefingController`). `ParseLogService` composes a Russian system prompt instructing the model
  to return a strict JSON object (`{contactName, subject, outcome, occurredAt}`) and calls the
  same `IOpenAiChatService.GenerateTextAsync` one-shot completion used by the briefing feature.
  The response is parsed with `JsonDocument.Parse`; a non-JSON or non-object response throws
  `InvalidOperationException` (mapped to `503` by the controller, same as an unconfigured/failed
  OpenAI call). Individual fields degrade gracefully instead of failing the whole parse:
  `contactName`/`occurredAt` are `null` when absent or (for the date) unparseable via
  `DateTime.TryParse`; `subject`/`outcome` default to an empty string if the model omits them.
- **company-service:** `POST /companies/{id}/logs/parse` (`CompanyController.ParseCallLog`) checks
  company ownership (`404` on foreign/unknown id, same as every other company endpoint), forwards
  `{rawText}` to ai-service via `ParseLogAiClient` (mirrors `BriefingAiClient`: typed `HttpClient`,
  `AiService:BaseUrl`/`ParseLogPath` config, `X-Internal-Service-Secret` header), and returns the
  parsed draft as-is — **no database write**. `503` if ai-service is unreachable, misconfigured, or
  returns an unparseable response. See `docs/API_CONTRACTS.md` for the full contract.
- **Frontend:** a "Вставить заметки" toggle inside `CallLogForm`
  (`components/call-log-form.tsx`), offered only when creating a new entry (not while editing).
  Switches the form to a single textarea; "Распознать" calls `useParseCallLog`
  (`hooks/use-parse-call-log.ts`, POST to `/companies/{id}/logs/parse`) and, on success, prefills
  the contact/subject/outcome/date fields and switches back to the normal manual form for
  review/edit before the user clicks "Сохранить запись" (the existing `POST /companies/{id}/logs`
  flow — parsing never saves anything itself). On AI failure the form stays in paste mode with an
  inline error plus a toast, and a "Заполнить вручную" link always lets the user bail out to the
  normal fields — the form is never blocked by an AI outage.

## Stage B remainder (planned, not implemented)

Approved 2026-07-09, tracked as Phase 39.14–39.17 in `docs/ROADMAP.md`: AI persona generation for
practice calls, voice-memo → log transcription, and an AI readiness score. None of this is
implemented yet — see the roadmap for scope and sequencing.
