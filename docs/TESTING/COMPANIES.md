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
excluded), cascade delete of logs + practice calls when a company is deleted.

### Backend — ai-service company-context (NUnit)
`src/backend/ai-service/Ai.Tests/Unit/CompanyContextDialogTests.cs`
```
cd src/backend
dotnet test ai-service/Ai.Tests/Ai.Tests.csproj --filter "FullyQualifiedName~CompanyContext"
```
Coverage: prompt composition appends the company block to both chat and feedback system prompts
when `companyContext` is present, prompt is unchanged when it's absent, `companyContext` combined
with a non-`company-call` mode is rejected, and the context is persisted on the Mongo
`DialogSession` document.

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
- `CompaniesPage.test.tsx` — list rendering, empty/loading/error states, create modal
- `useCompanies.test.tsx` — list/create/update/delete hooks, search filtering
- `useCompanyLogs.test.tsx` — call-log CRUD hooks
- `CompaniesFormat.test.ts` — description excerpt / date formatting helpers
- `CompaniesTimeline.test.ts` — `mergeTimeline`/`filterTimeline` ordering and segment filtering
- `CompanyPage.test.tsx` — detail page: description edit, pre-call panel, timeline, delete flow
- `CompanyVoiceCallPage.test.tsx` — goal handoff (sessionStorage/query fallback), call states,
  practice-call creation on session start, quota/mode-unavailable error states
- `CompanyChatCallPage.test.tsx` — chat variant equivalent of the above

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
