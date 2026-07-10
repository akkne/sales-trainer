# Testing — Companies (Компании)

Covers Stage A (Phase 39.1–39.7): company-service, ai-service company-context sessions, and the
`/companies` frontend. Feature doc: [docs/COMPANIES/COMPANIES.md](../COMPANIES/COMPANIES.md).
Design spec: [docs/COMPANIES/DESIGN_SPEC.md](../COMPANIES/DESIGN_SPEC.md).

## Automated

### Backend — company-service (NUnit, in-memory/SQLite EF provider)
`src/backend/company-service/Company.Tests/Unit/CompanyServiceTests.cs`
```
cd src/backend
dotnet test company-service/Company.Tests/Company.Tests.csproj
```
Coverage: list/search companies (own only), create/get/update/delete company, description-excerpt
truncation, call-log create/list/update/delete (ownership-scoped, 404 for foreign/unknown company
or log id), practice-call create/list, recent-goals (last 5 distinct, newest first, empties
excluded), cascade delete of logs + practice calls when a company is deleted, status pipeline
(defaults to `Lead` on create, `PUT /companies/{id}/status` updates status for the correct owner,
`404` for wrong owner/nonexistent company, status is included in both list and detail reads),
follow-up reminders (`PUT /companies/{id}/follow-up`: schedules for the correct owner, `404` for
wrong owner/nonexistent company, rescheduling resets `FollowUpNotifiedAt` to `null`, clearing
`nextActionAt` clears note + notified-at together, persists across reads, `nextActionAt` included
on the list DTO).

`src/backend/company-service/Company.Tests/Unit/CompanyFollowUpJsonTests.cs` — wire-format tests:
`CompanyDetailDto`/`CompanySummaryDto` serialize the follow-up fields (including `null`),
`UpdateCompanyFollowUpRequestDto` deserializes a valid payload and a null-clearing payload.

`src/backend/company-service/Company.Tests/Unit/FollowUpReminderServiceTests.cs` — the
due-poll/claim/publish loop (`FollowUpReminderService`, in-memory EF + a mocked `IEventPublisher`):
publishes for a due+unnotified company with the correct payload/topic/partition-key, skips a
future follow-up, skips a company with no follow-up scheduled, **once-only guard** skips an
already-notified due company, a claimed company is not re-published on a second poll tick,
`FollowUpNotifiedAt` is persisted on the claimed company, multiple due companies are processed in
one tick.

`CompanyServiceTests.cs` also covers the AI briefing feature (Phase 39.12, `IBriefingAiClient`
mocked with NSubstitute): `GenerateBriefingAsync`/`GetBriefingAsync` `404` for wrong owner/
nonexistent company, generation caches `content`/`generatedAt` on the company and returns them,
the AI request includes the company description + single most recent non-empty practice-call goal
+ recent call-log entries, `GetBriefingAsync` returns both fields `null` before the first
generation and the cached values afterward.

`CompanyServiceTests.cs` also covers AI call-log parsing (Phase 39.13, `IParseLogAiClient` mocked
with NSubstitute): `ParseCallLogAsync` returns `null` for wrong owner/nonexistent company (no AI
call made — doesn't persist anything either way), returns the AI client's parsed fields as-is,
passes `rawText` through unchanged, and propagates an `InvalidOperationException` thrown by the AI
client (mapped to `503` by `CompanyController.ParseCallLog`).

`CompanyServiceTests.cs` also covers personas (Phase 39.14, `IPersonaAiClient` mocked with
NSubstitute): `CreatePersonaAsync`/`ListPersonasAsync`/`DeletePersonaAsync` ownership-scoped
(`404`/`null`/`false` for wrong owner or nonexistent company), a persona from another company is
not deletable via a different company id, `ListPersonasAsync` returns newest-first,
`GeneratePersonaAsync` returns `null` for wrong owner/nonexistent company (no AI call made, no
persist either way), passes the company's `description` plus the seed contact name/position and
difficulty through to the AI client unchanged, and propagates an `InvalidOperationException`
thrown by the AI client (mapped to `503` by `CompanyController.GeneratePersona`).
```
cd src/backend
dotnet test company-service/Company.Tests/Sellevate.Company.Tests.csproj
```

### Backend — building-blocks (NUnit)
`src/backend/building-blocks/BuildingBlocks.Tests/{TopicsCatalogTests,EventContractCatalogTests}.cs`
```
cd src/backend
dotnet test building-blocks/BuildingBlocks.Tests/Sellevate.BuildingBlocks.Tests.csproj
```
Coverage: `Topics.CompanyFollowUpDue` (`company.followup.due`) is in the reflected `Topics.All`
catalogue the startup provisioner uses; the event contract test asserts the camelCase wire shape
(`companyId, userId, companyName, nextActionAt, note`) producers/consumers agree on.

### Backend — notification-service (NUnit)
`src/backend/notification-service/Notification.Tests/Unit/NotificationEventMapperTests.cs`
```
cd src/backend
dotnet test notification-service/Notification.Tests/Sellevate.Notification.Tests.csproj --filter "FullyQualifiedName~CompanyFollowUpDue"
```
Coverage (consumer → mapper): `company.followup.due` maps to an **in-app-only**
(`SendEmail: false`) `CompanyFollowUpDue` notification with the company name interpolated into
the title, the note (or a default fallback when absent) as the body, `actionUrl` `/companies/{id}`,
and dedupes on `companyId:nextActionAt` (not just `companyId`) so a rescheduled due date still
produces a fresh notification; a blank company name is rejected (mapper returns `null`).

### Backend — ai-service company-context (NUnit)
`src/backend/ai-service/Ai.Tests/Unit/CompanyContextDialogTests.cs`
```
cd src/backend
dotnet test ai-service/Ai.Tests/Ai.Tests.csproj --filter "FullyQualifiedName~CompanyContext"
```
Coverage: prompt composition appends the company block to both chat and feedback system prompts
when `companyContext` is present, prompt is unchanged when it's absent, `companyContext` combined
with a non-`company-call` mode is rejected, and the context is persisted on the Mongo
`DialogSession` document. Also covers persona injection (Phase 39.14): when a persona is present,
the chat prompt contains the "ВОЙДИ В РОЛЬ" role-play instruction plus all persona fields and a
difficulty description, the feedback prompt contains the persona-awareness block (distinct wording
from chat) plus the same fields, and — critically — the no-persona chat prompt is asserted
**byte-for-byte equal** to the exact pre-39.14 output string, proving the persona addition cannot
silently change behavior when no persona is selected.

### Backend — ai-service briefing (NUnit)
`src/backend/ai-service/Ai.Tests/Unit/BriefingServiceTests.cs`,
`src/backend/ai-service/Ai.Tests/Unit/BriefingControllerTests.cs`
```
cd src/backend
dotnet test ai-service/Ai.Tests/Ai.Tests.csproj --filter "FullyQualifiedName~Briefing"
```
Coverage: `BriefingService` returns the chat service's markdown content, throws
`InvalidOperationException` when OpenAI isn't configured, the composed system prompt includes the
company description/goal/recent-calls/feedback-summaries, and empty recent-calls/feedback lists
don't throw. `BriefingController` (`IBriefingService` mocked): `200` with `{content, generatedAt}`
on success, `503` on `InvalidOperationException` or `HttpRequestException` — same pattern as
`EvaluationController`.

### Backend — ai-service call-log parsing (NUnit)
`src/backend/ai-service/Ai.Tests/Unit/ParseLogServiceTests.cs`,
`src/backend/ai-service/Ai.Tests/Unit/ParseLogControllerTests.cs`
```
cd src/backend
dotnet test ai-service/Ai.Tests/Ai.Tests.csproj --filter "FullyQualifiedName~ParseLog"
```
Coverage: `ParseLogService` returns the parsed `{contactName, subject, outcome, occurredAt}` from
well-formed AI JSON, returns `null` for `occurredAt` when the date is missing or unparseable
(`DateTime.TryParse` failure) without throwing, defaults `subject`/`outcome` to an empty string
when the AI omits them, throws `InvalidOperationException` when the AI response is non-JSON or a
non-object JSON value (mirrors the graceful-degrade philosophy of
`AiEvaluationStrategyBase.ParseAiResponse` but surfaces failure as a thrown exception instead of a
degraded-but-valid result, since parse-log has no meaningful "failed but valid" shape), and throws
when OpenAI isn't configured. `ParseLogController` (`IParseLogService` mocked): `200` with the
parsed DTO on success, `400` when `rawText` exceeds the 16000-char guard (service not called),
`503` on `InvalidOperationException` or `HttpRequestException` — same pattern as
`BriefingController`.

### Backend — ai-service persona generation (NUnit)
`src/backend/ai-service/Ai.Tests/Unit/PersonaServiceTests.cs`,
`src/backend/ai-service/Ai.Tests/Unit/PersonaControllerTests.cs`
```
cd src/backend
dotnet test ai-service/Ai.Tests/Ai.Tests.csproj --filter "FullyQualifiedName~Persona"
```
Coverage: `PersonaService` returns the parsed `{name, position, personality}` from well-formed AI
JSON (including when fenced in a ```` ```json ```` code block), throws
`InvalidOperationException` when the AI response is non-JSON/non-object, when any of
`name`/`position`/`personality` is missing or blank (personas have no meaningful partial result,
unlike parse-log's optional fields), and when OpenAI isn't configured; the composed system prompt
varies by requested difficulty (asserted via a difficulty-parameterized test). `PersonaController`
(`IPersonaService` mocked): `200` with the persona DTO on success, `400` when `companyDescription`
exceeds the 16000-char guard (service not called), `503` on `InvalidOperationException` or
`HttpRequestException` — same pattern as `ParseLogController`.

### Backend — gateway route flip (NUnit)
`src/backend/gateway/Gateway.Tests/CompanyRouteFlipTests.cs`
```
cd src/backend
dotnet test gateway/Gateway.Tests/Sellevate.Gateway.Tests.csproj --filter "FullyQualifiedName~Company"
```
Coverage: `/companies/{**catch-all}` and root `/companies` route to the `company` cluster (not the
monolith), cluster destination resolves to the `company` container address.

### Frontend (Vitest)
```
cd src/frontend
npx vitest run __tests__/Compan
```
- `CompaniesPage.test.tsx` — list rendering, empty/loading/error states, create modal, status
  filter chips (filter/clear)
- `useCompanies.test.tsx` — list/create/update/delete hooks, search filtering,
  `useUpdateCompanyStatus` (PUT to `/companies/{id}/status`, cache invalidation, error toast),
  `useUpdateCompanyFollowUp` (PUT to `/companies/{id}/follow-up` with a schedule and with
  null-clearing fields, cache invalidation, error toast)
- `CompanyStatusMenu.test.tsx` — status dropdown: shows current status, lists all 5 options,
  calls `onChange` on selection (not on re-selecting the current status), disabled state
- `CompanyFollowUpCard.test.tsx` — empty state, shows scheduled date/note, edit → save (date +
  note), save disabled while date is empty, clear via "Убрать напоминание", cancel discards edits
- `useCompanyBriefing.test.tsx` — `useCompanyBriefing` (GET, normalizes a 204 to
  `{content: null, generatedAt: null}`, disabled without a companyId), `useGenerateCompanyBriefing`
  (POST with an empty body, writes the response into the query cache, error toast on failure)
- `CompanyBriefingCard.test.tsx` — loading state, empty state with a generate button, renders
  markdown content + relative generated-at timestamp, regenerate button, generating/disabled
  state, error message shown both alongside existing content and in the empty state
- `useParseCallLog.test.tsx` — `useParseCallLog` posts `{rawText}` to
  `/companies/{id}/logs/parse` and returns the parsed fields, error toast on failure
- `CallLogForm.test.tsx` — paste-notes mode: "Вставить заметки" toggle is offered only when
  creating a new entry (not while editing), switching to paste mode and parsing prefills
  contact/subject/outcome/date and returns to the manual form for review, a `null` `contactName`
  from the AI leaves the field untouched, an AI failure keeps the form in paste mode with an
  inline error and a "Заполнить вручную" escape hatch that leaves the form fully usable, and
  toggling back to manual mode without parsing makes no API call. Voice memo recording (Phase
  39.15, MediaRecorder + `getUserMedia` mocked): record → stop → transcribe lands the transcript
  in the raw-notes textarea; appends with a `\n` separator instead of clobbering existing text;
  microphone-permission-denied shows an inline error and leaves the field editable; a
  transcription API error shows an inline error and leaves the field editable; the mic button is
  hidden entirely when `MediaRecorder` is unsupported
- `CompaniesFollowUp.test.ts` — `getFollowUpTone`: null when unscheduled, `overdue` for a past
  date, `due` for within-24h and exactly-now, `null` beyond 24h, `null` for an invalid date string
- `useCompanyLogs.test.tsx` — call-log CRUD hooks
- `CompaniesFormat.test.ts` — description excerpt / date formatting helpers
- `CompaniesTimeline.test.ts` — `mergeTimeline`/`filterTimeline` ordering and segment filtering
- `CompanyPage.test.tsx` — detail page: description edit, pre-call panel, timeline, delete flow
- `CompanyVoiceCallPage.test.tsx` — goal handoff (sessionStorage/query fallback), call states,
  practice-call creation on session start, quota/mode-unavailable error states
- `CompanyChatCallPage.test.tsx` — chat variant equivalent of the above, plus (Phase 39.14): a
  persona stored in `sessionStorage` under `company-call-persona:{companyId}` is spread into the
  `companyContext` sent to `POST /dialog/sessions` as `personaName`/`personaPosition`/
  `personaPersonality`/`personaDifficulty`
- `useCompanyPersonas.test.tsx` — persona CRUD hooks (`useCompanyPersonas` GET,
  `useAddCompanyPersona`/`useDeleteCompanyPersona` POST/DELETE with query invalidation and error
  toasts) and `useGenerateCompanyPersona` (POST to `/companies/{id}/personas/generate`, returns the
  draft, error toast on AI failure)
- `PrecallPanel.test.tsx` — persona chips (default «Без персоны» selection, selecting/deselecting
  a saved persona feeds the right `SelectedPersona` object into `onCall`/`onChat`), generate flow
  (opens the generate form, calls `onGeneratePersona` with the seed/difficulty, renders the draft,
  "Сохранить собеседника" calls `onSavePersona`), and the inline error shown when generation fails

## Manual checklist

### Company CRUD + validation
1. `/companies`: «Добавить компанию» with only a name → company created, opens detail page.
2. Create with name over 200 chars → rejected (client and/or server validation error shown).
3. Edit description on the detail page to something near 8000 chars → saves; over the limit is
   rejected.
4. Edit name + description via the edit modal → list and detail both reflect the update
   (`updatedAt` bumps, list re-sorts by most-recently-updated).
5. Delete a company → confirm dialog names the company and warns description/practice
   history/call log are all removed; after confirming, redirected to `/companies` and the row is
   gone.

### Ownership isolation
6. Log in as user A, create a company; log in as user B (or an incognito session) and confirm the
   company does **not** appear in B's `/companies` list.
7. As user B, navigate directly to A's company id (`/companies/{a-company-id}`) → "Компания не
   найдена" (404), not a data leak or a 403/500.
8. Same isolation check for a call-log entry id and a practice-call id belonging to another user.

### Search
9. Create 2+ companies with distinguishable names; type a partial match into the search box on
   `/companies` → only matching rows remain; clearing the search restores the full list.

### Pre-call goal + recent goals
10. On a company with zero practice calls, the pre-call panel shows no "Недавние цели" chips.
11. Run a practice call with a goal, return to the company page → the goal appears as a
    recent-goal chip; clicking it fills the goal input.
12. Run 6+ distinct-goal practice calls on the same company → recent-goals list caps at 5, newest
    first.

### Voice call end-to-end
13. Fill in a company description with a distinctive detail (e.g. a product name) and start a
    voice call with a goal → the AI's opening line/behavior reflects the description (persona
    speaks as if working at that company) — this is the key regression check for the
    company-context prompt injection.
14. Set an explicit call goal (e.g. "договориться о демо") → the AI's feedback at the end
    references progress against that specific goal, not a generic dialog-mode rubric.
15. Leave the goal blank → call still starts; feedback doesn't reference a goal.
16. Hang up mid-call → feedback modal appears; closing it returns to the company page.
17. Exhaust the daily/monthly voice-minute quota (or check with a test account near the limit) →
    starting a company call surfaces the same 429 quota error as a normal voice dialog.

### Chat variant
18. Repeat the goal + description checks (13–15) via `/companies/{id}/call/chat` instead of voice
    — same context injection, same feedback-on-goal behavior, text-based UI.

### Practice call in timeline
19. After a completed voice or chat practice call, it appears at the top of the company's
    timeline immediately (no manual refresh) with a feedback summary.

### Real-call log CRUD
20. «Записать звонок» with only "с кем" filled (blank subject/outcome) → saves successfully.
21. Try to save with "с кем" empty → rejected client-side.
22. Edit an existing log entry (all 3 fields + date) → saves, timeline entry updates in place.
23. Delete a log entry → confirm dialog, entry removed from the timeline.
24. Log an entry with an "occurred at" date near midnight in a non-UTC local timezone → the date
    shown in the timeline matches the date the user picked (not shifted a day by UTC conversion).

### Segmented timeline filter
25. With both practice calls and real-call logs present, toggle Все / Тренировки / Звонки →
    each segment shows only its own entries, ordering preserved within each.
26. While "Тренировки" is selected, the «Записать звонок» button is hidden (nothing to log against
    a training-only view).

### Delete cascades
27. Delete a company that has both call-log entries and practice calls → both disappear (verify
    via a fresh `GET /companies/{id}/logs` and `/practice-calls` returning 404 once the company
    itself is gone).

### Status pipeline
32. New company defaults to «Лид» — visible as the status chip on both the list row and the
    company header.
33. On the company header, click the status chip → dropdown opens with all 5 statuses; the
    current one shows a checkmark. Pick a different status → header chip and list-row chip both
    update (list re-fetches on navigating back), correct tone color per status (neutral / info /
    violet / success / danger for Лид / Был контакт / Встреча назначена / Сделка закрыта / Отказ).
34. On `/companies`, click a status filter chip → only companies with that status remain; click
    the same chip again → filter clears and the full list returns. Click «Все» → clears any
    active status filter.
35. Combine a status filter with the name search → both filters apply together (client-side, no
    extra network calls — verify via network tab that filtering doesn't re-hit `GET /companies`).
36. Reload `/companies/{id}` after changing status → the new status persists (confirms
    `PUT /companies/{id}/status` was saved server-side, not just client cache).

### Follow-up reminders
37. On the company page, «Следующий контакт» card → «Запланировать», pick a date + write a note,
    save → card shows the date and note; list row for that company shows a due/overdue badge once
    the date is within 24h or past.
38. Schedule a date more than 24h out → no badge on the row or card yet (badge only appears once
    within the due window).
39. Set a `NextActionAt` in the past directly in the DB (or via the API with a past date), wait
    for the reminder poll (`FollowUpReminder:PollIntervalMinutes`, default 5 min — lower it in
    local dev config to test faster) → a new in-app notification appears in the bell/inbox titled
    «Пора связаться с {имя компании}», clicking it navigates to `/companies/{id}`.
40. After the reminder fires, reschedule the same company's follow-up to a new future date → wait
    for that date to become due → **a second, distinct notification appears** (confirms the
    reschedule reset the once-only guard server-side, not just refreshed the UI).
41. Click «Убрать напоминание» while editing a scheduled follow-up → card returns to the empty
    "Запланировать" state, row badge disappears; reload the page to confirm it persisted
    server-side (not just local state).
42. Edit an existing follow-up's note only (same date) → save → note updates without resetting
    the due/overdue badge state.

### AI pre-call briefing
43. On a company with no briefing yet, the "Шпаргалка к звонку" card shows the empty state with a
    «Сгенерировать» button (`GET /companies/{id}/briefing` returns `204`).
44. Click «Сгенерировать» → button shows a generating state, then the card renders the markdown
    cheat sheet (кто они / о чём договаривались / возможные возражения / следующий шаг) with a
    relative "Обновлено ..." timestamp.
45. Reload the page → the same briefing is shown immediately (`GET` returns the cached content,
    no regeneration).
46. Add a real call-log entry and/or a practice call with a goal, then click «Обновить» → the new
    briefing reflects the added context (mentions the logged call or goal); the cached content
    replaces the previous one (no history of past briefings).
47. Simulate ai-service unavailable (stop ai-service or unset the OpenAI key) → «Сгенерировать»/
    «Обновить» shows an error message without crashing the page; any previously cached briefing
    stays visible if one existed.

### AI real-call log parsing / "Вставить заметки"
48. On the company page, click «+ Записать звонок» → click «Вставить заметки» → the form switches
    to a single textarea for pasted notes/transcript; a «Заполнить вручную» link is visible.
49. Paste a few sentences of notes/transcript that mention a contact name, what was discussed, an
    outcome, and a date (e.g. "Звонили Ивану, обсудили условия поставки, договорились созвониться
    на следующей неделе, 5 июля") → click «Распознать» → button shows a "Распознаём…" state, then
    the form returns to the normal manual fields with «С кем говорил» / «О чём был разговор» /
    «К чему пришли» / «Дата» prefilled from the notes.
50. Edit any prefilled field before saving (e.g. correct a misheard name) → click «Сохранить
    запись» → the entry is saved via the normal `POST /companies/{id}/logs` flow and appears in
    the timeline with the edited values (confirms parsing never persists anything on its own —
    only the explicit save does).
51. Paste notes with no date mentioned → after «Распознать», the «Дата» field is left at its
    default (today) rather than showing an error — a missing date doesn't block the rest of the
    prefill.
52. Simulate ai-service unavailable (stop ai-service or unset the OpenAI key), then click
    «Распознать» → an inline error message appears under the textarea plus a toast, the form stays
    in paste mode with the pasted text intact, and «Заполнить вручную» still switches to a fully
    usable empty manual form (confirms the AI outage never blocks manual log entry).
53. While editing an existing log entry (not creating a new one), confirm «Вставить заметки» is
    not offered — paste-notes mode is create-only.

### Voice memo → log (Phase 39.15)
53a. On the company page, click «+ Записать звонок» → click «Вставить заметки» → a mic button
     («Наговорить») is visible next to the raw-notes label.
53b. Click «Наговорить» → grant microphone permission → the button switches through "Запрос
     доступа…" → "Остановить" (recording). Speak a few sentences about a call, then click
     «Остановить» → button shows "Распознаём…" briefly, then the transcript appears in the
     raw-notes textarea.
53c. With the transcript in the textarea, click «Распознать» → the AI parse (39.13) runs on the
     transcribed text exactly as it would on pasted text, prefilling contact/subject/outcome/date
     for review before «Сохранить запись».
53d. Record a second memo after the first transcript is already in the textarea → confirm the new
     transcript is appended on a new line rather than replacing the earlier text.
53e. Deny the microphone permission prompt → an inline error message appears (e.g. "Доступ к
     микрофону запрещён") and the raw-notes textarea remains fully editable for manual/paste
     entry.
53f. Simulate a transcription failure (stop ai-service or disconnect network mid-recording) →
     after clicking «Остановить», an inline error appears and the raw-notes textarea keeps
     whatever text was already there, still editable.
53g. In a browser/context without microphone support (or with `MediaRecorder` unavailable), the
     mic button is not shown at all — paste and manual entry remain fully usable.

### AI persona generation for practice calls
54. On the company page's pre-call panel, the persona row defaults to «Без персоны» selected (no
    saved personas yet) — starting a call behaves exactly as before 39.14 (no persona in the
    prompt).
55. Click «Сгенерировать собеседника» → a small form opens (optional contact name/position +
    Easy/Medium/Hard difficulty chips, defaulting to Средний) → click «Сгенерировать» → button
    shows a generating state, then a draft persona (name, position, personality) renders inline
    with a «Сохранить собеседника» button.
56. Generate at each difficulty (Лёгкий / Средний / Сложный) → the generated personality text
    plausibly reflects the requested toughness (friendlier/more agreeable for Easy, more
    skeptical/objection-heavy for Hard) — spot-check, not exact wording.
57. Click «Сохранить собеседника» on a generated draft → it appears as a new chip in the persona
    row (persisted — reload the page and confirm the chip is still there via
    `GET /companies/{id}/personas`).
58. Select a saved persona chip, then «Позвонить» → start a voice call and confirm the AI answers
    **in character** as that persona (uses the generated name/position implicitly in tone,
    reflects the personality/toughness) rather than a generic company employee — the key
    regression check for persona prompt injection.
59. Repeat 58 via «Чат» instead of «Позвонить» — same in-character behavior over text.
60. Select «Без персоны» after having a persona selected, then start a call → AI plays a generic
    company employee as before (no persona leaks into the prompt from a stale selection).
61. Prefill the seed contact name/position from an existing saved contact before generating → the
    generated persona's position is plausibly related to the seed (not required to match exactly
    — the AI takes it only as inspiration).
62. Simulate ai-service unavailable during generation → «Сгенерировать» shows an inline error
    (toast + message), the generate form stays open for retry, and the rest of the pre-call panel
    (goal input, «Без персоны», existing saved persona chips, «Позвонить»/«Чат») remains fully
    usable.
63. Delete a saved persona (via its chip or a management surface, if present) → it disappears from
    the chip row; a call started right after with «Без персоны» selected is unaffected.

### Mobile nav
28. On a narrow viewport, the bottom nav shows «Компании» in the 5-slot bar and does **not** show
    «Справочник»; the guidebook is still reachable from the desktop nav rail at wider viewports.

### Dark theme
29. Toggle dark mode on `/companies` and `/companies/[id]` (list rows, description card, pre-call
    panel, timeline items, modals) → no unreadable text/contrast regressions.

### Error states
30. Simulate a company-service outage (stop the service or block the gateway route) → `/companies`
    shows a retry-capable error state, not a blank page or an unhandled exception.
31. Simulate ai-service unavailable (`IsOpenAiConfigured` false, or stop ai-service) while opening
    a call page → "Тренировочные звонки недоступны" card shown instead of a stuck spinner.
