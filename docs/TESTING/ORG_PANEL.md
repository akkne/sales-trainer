# Testing — the organization panel (`/org/*`, block 40.20)

What the panel's shell must do and how each part of it is checked. The design it verifies is
[docs/TENANCY/ADMIN_UI_DESIGN.md](../TENANCY/ADMIN_UI_DESIGN.md); the split it documents is
[docs/ADMIN_PANEL.md → Two panels](../ADMIN_PANEL.md#two-panels-4020).

Everything runs from `src/frontend`:

```
npx tsc --noEmit
npx vitest run
```

Screens O1–O19 land in slices 1–11 and each slice adds its own section below. **Slice 0 — the
shell — is what this document covers today.**

---

## Automated (vitest)

| File | Covers |
|---|---|
| `__tests__/legacyAdminRedirects.test.ts` | the §1.5 redirect table |
| `__tests__/organizationRoleGating.test.ts` | `isOrganizationStaff`, `canManageOrganizationPeople`, the panel gate as a union of the two axes |
| `__tests__/completionRule.test.ts` | `describeCompletionRule`, `daysUntilDeadline` after the lift into `features/assignments/utils/` |
| `__tests__/orgNavigation.test.ts` | all nine nav entries, their labels, icons, the three badges, active-route matching, and that nothing mentions XP/streaks/leagues |
| `__tests__/OrgSharedComponents.test.tsx` | the seven new `shared/components` — `Modal`, `ConfirmDialog`, `DataTable`, `EmptyState`, `PageHeader`, `Tabs`, `MetricBar` |

The behaviours worth naming, because they are the ones that break quietly:

- **The two live notification links.** `/admin/assignments/{id}?action=remind&scope=not_started`
  → `/org/assignments/{id}?action=remind&scope=not_started`, and
  `/admin/dialog-reviews?note={id}` → `/org/reviews?note={id}`. The query survives whole.
- **Longest prefix wins.** `/admin/dialog/overrides` redirects, `/admin/dialog` and
  `/admin/dialog/{bundleId}` do not — that one is a platform screen.
- **No false positives.** `/admin/teams` and `/admin/assignments-archive` are not redirected;
  matching is on a whole path segment, not on a string prefix.
- **An unknown completion-rule kind renders as nothing**, never as a guess.
- **A zero badge is omitted**, on the sidebar and on `Tabs` alike.
- **`MetricBar` never draws its fill past the track** when the organization is over quota, and
  draws an empty track with a `—` when no ceiling is configured.
- **`DataTable` shows a skeleton while loading**, the caller's empty state when there is nothing,
  and never an empty `<table>`.
- **`Modal` closes on Escape and on the close button**, reports `role="dialog"` +
  `aria-modal="true"`, and is labelled by its title.

---

## Manual — the shell

Preconditions: the app running (`scripts/dev-up.sh`), and three accounts —
a `Manager`, a `TenancyAdmin`, and a platform `SuperAdmin` with no membership.

### The gate

| As | Open | Expect |
|---|---|---|
| not logged in | `/org` | redirected to `/login` |
| `role=User`, `orgRole=Manager` | `/org` | redirected to `/tree`; a warn line in the client log |
| `role=User`, `orgRole=TenancyAdmin` | `/org` | the panel, sidebar and all |
| `role=SuperAdmin`, no membership | `/org` | state O0, not an empty table and not an error |
| `role=SuperAdmin`, impersonating | `/org` | the panel, with the impersonation banner naming the organization |
| `role=User`, `orgRole=TenancyAdmin` | `/admin` | redirected to `/tree` — the two panels are separate gates |

### State O0

As the platform superadmin with no membership, open `/org`:

- The card reads «Панель организации открывается изнутри» and explains that platform roles are not
  bound to organizations.
- «Открыть реестр организаций» goes to `/admin/organizations`.
- No sidebar, no empty tables, no red error banner.
- Impersonate a customer from the registry → `/org` now shows the panel and the banner.

### Navigation

As a `TenancyAdmin`:

- Nine entries, in order: Команда, Задания, Разговоры, Спорные оценки, Контент, Профиль компании,
  Программа, Люди, Расход ИИ.
- «Команда» is highlighted on `/org` and **not** on `/org/assignments`.
- «Задания» stays highlighted on `/org/assignments/new`.
- The footer has «В приложение» → `/tree`, and «Платформенная админка» → `/admin` **only** for
  platform staff.
- At a phone width the sidebar becomes a drawer; the hamburger opens it, the backdrop and any nav
  link close it.
- Entries whose slice has not shipped yet lead to a 404. Expected until slices 1–11 land.

### The three badges

- Issue an assignment → «Задания» shows the count within a minute, or immediately on returning to
  the tab.
- Dispute a dialog score as a manager → «Спорные оценки» shows the count.
- With a stale override present, «Контент» shows an amber dot and **no number**.
- Stop ai-service → the dot must **not** light from its failure alone; a counter that cannot be
  read contributes nothing.

### The reminder link from a notification

1. As a `TenancyAdmin`, open `/admin/assignments/<any-id>?action=remind&scope=not_started`.
2. The address bar becomes `/org/assignments/<id>?action=remind&scope=not_started`.
3. **No reminder is sent by the load.** Check the notification service: nothing was queued.
4. Back returns to wherever you were before, not to the `/admin/*` address.
5. Repeat as a platform superadmin inside impersonation — same destination.
6. Open `/admin/dialog-reviews?note=<id>` → `/org/reviews?note=<id>`.
7. Open `/admin/dialog` → the platform dialog-bundles screen, unredirected.
8. Open `/admin/nonsense` → 404, not a redirect and not a blank page.

### The rail entry

- As a `TenancyAdmin` in the main app, the left rail shows a briefcase «Управление» above
  «Настройки»; it opens `/org`.
- As a plain `Manager`, the entry is absent.
- The mobile bottom navigation is unchanged — the panel is reached by address on a phone.

### No gamification

Walk every screen of the panel: no XP, no streaks, no leagues, no «очки прогресса», even on
screens whose endpoint returns `xpEarned`. The only numbers a РОП sees are accuracy in percent and
a dialog score out of 100.

---

## Slice 9 — «Расход ИИ» (O17, `/org/usage`)

What it covers: `app/(org)/org/usage/page.tsx` and `features/org-usage/**`. Design:
[docs/TENANCY/ADMIN_UI_DESIGN.md → O17](../TENANCY/ADMIN_UI_DESIGN.md#o17--orgusage--расход-ии).
Semantics: [docs/AI_QUOTAS.md](../AI_QUOTAS.md).

**Endpoint.** `GET /admin/ai-usage` only — it takes no parameters, always the current UTC calendar
month. The screen is **read-only by design**: `GET`/`PUT /admin/ai-quota` are
`RequirePlatformAdministrator` and are never called from here — a quota is what the organization
bought, not something a РОП raises on themself.

### Automated (vitest, from `src/frontend`)

```
npx tsc --noEmit
npx vitest run
```

| File | Covers |
|---|---|
| `__tests__/orgUsageFormatting.test.ts` | `formatModelCost`, `formatTotalCost`, `formatUsagePeriodLabel`, `formatWholeNumberRu`, `pluralizeRussianCount`/`describeCallCount`, `resolveMetricTone`/`hasConfiguredLimit`, `describeUsageKind`, and the `QUOTA_STATE_BANNER_COPY` dictionary |
| `__tests__/orgUsageComponents.test.tsx` | `ModelUsageTable`, `QuotaMeters`, `QuotaStateBanner` rendered with `@testing-library/react` |

The behaviours worth naming:

- **`estimatedCost: null` renders as «нет цены», never as `"0 ₽"`.** Pinned twice: once on the
  formatter (`formatModelCost(null, ...)`) and once end-to-end through `ModelUsageTable`, which
  also asserts `"0 ₽"`/`"0,00 ₽"` do **not** appear on that row. A genuinely free (`0`) line is
  covered separately and must render `"0,00 ₽"` — the two cases are different and must not collapse
  into each other.
- **The report-level total** reads `TOTAL_COST_UNAVAILABLE_LABEL` — «Итоговая стоимость не
  считается: для части моделей не задана цена.» — whenever `hasUnpricedModels` is true, regardless
  of what `estimatedCost` happens to hold; the flag is authoritative, not the null check alone.
- **A limit of `0` draws no bar.** `hasConfiguredLimit` and `QuotaMeters` render «без лимита» as
  plain text instead of an empty `MetricBar` — a zero-percent bar and "no ceiling configured" are
  different statements, per docs/AI_QUOTAS.md §2.
- **`quotaState: "ok"` renders no banner at all** — `QuotaStateBanner` returns `null`, not an empty
  card. `batch_paused` and `exhausted` render visibly different copy: the former says conversations
  keep working while background generation stopped; the latter says both are down.
- **Russian call-count pluralization.** `describeCallCount` agrees with the design mock's own two
  examples, which disagree with each other on purpose: `"231 вызов"` (one-form) next to
  `"1 610 вызовов"` (many-form), plus the 11–14 exception (`"11 вызовов"`, not `"11 вызов"`).
- **`llmEstimatedCallCount` is never rendered as a second number** next to `llmCallCount` — it does
  not appear anywhere in `features/org-usage/**` outside its own type definition and a comment
  explaining why.
- **A fresh organization with no spend** (`models: []`) shows `ModelUsageTable`'s own `EmptyState`
  («В этом месяце расхода ещё не было») rather than a bare table; the three meters still render at
  `0` against whatever limit is configured, which is the ordinary zero-usage case, not an empty
  state of its own.

### Manual

Preconditions: a `TenancyAdmin` account, and a way to move an organization's `AiUsageRecords` /
`OrganizationQuotas` rows for the three non-`ok` states (or wait for real spend to cross them).

| Scenario | Expect |
|---|---|
| Open `/org/usage` on a slow connection | three skeleton blocks, then the real content — no flash of an empty table |
| Stop ai-service, open `/org/usage` | `ErrorState` with a working «Повторить» button; clicking it refetches |
| Fresh organization, no usage this month | meters at 0 against the configured (or default) limits, no banner, model table shows its empty state |
| Organization past `SoftWarningPercent` | amber "Приближаетесь к лимиту" banner; token meter turns amber past 80% of its own limit |
| Organization past the batch ceiling | "Фоновая генерация приостановлена" banner; a manager can still hold a conversation |
| Organization at the monthly token limit | red "Лимит исчерпан" banner; per docs/AI_QUOTAS.md §5, interactive calls now 429 too |
| A model with no price-table entry was used this month | its row shows «нет цены»; the total line shows the "not calculated" sentence, not a partial figure |
| Try to find a quota-editing control anywhere on the screen | there is none — raising the limit is a platform-admin action on a different route entirely |

---

## Slice 2 — O1 «Команда» (`/org`)

The heat map, the suggestion panel above it, and the roster merge that feeds the rows.

### Automated (vitest)

| File | Covers |
|---|---|
| `__tests__/orgTeamHeatMap.test.ts` | the four-step colour scale and its boundaries, the dash for a withheld percentage, and column↔cell alignment on both axes |
| `__tests__/orgTeamRoster.test.ts` | `mergeTeamRoster` in all four roster conditions, the window summary maths, Russian pluralization, and the suppression-reason dictionary |
| `__tests__/OrgTeamScreen.test.tsx` | `SkillHeatMap` end to end: the states table of ADMIN_UI_DESIGN.md O1, and that no XP/streak/league word reaches the DOM |

The behaviours worth naming:

- **50 / 65 / 80 are the only boundaries.** `49→critical`, `50→weak`, `64→weak`, `65→plain`,
  `79→plain`, `80→strong`. Lime (`--primary`) appears nowhere on the map — «готово» is `--success`.
- **`accuracyPercent: null` is its own step**, drawn as «—» with an `aria-label` reading «меньше
  {minimumAttemptsForAccuracy} попыток». The threshold in that sentence comes from the response,
  never from a client constant. `null` and `0` must never render the same.
- **Rows are keyed, not positional.** A manager who skipped a stage keeps every other cell under
  the right column; the test pins the `[null, 41]` case that a positional read would print as
  `[41, null]`.
- **The roster merge is the one place this screen improves on its own design.** `skill-map` alone
  can never produce the «† уже не работает» row (when it reaches identity-service its member list
  *is* the active roster, so every `isActiveMember` is `true`; when it cannot, every one is `null`).
  `GET /memberships?status=all` restores both that mark and the newly-hired person with zero
  attempts. Four conditions are pinned: roster present, roster absent, roster absent but
  `rosterKnown: true`, and a member the roster does not mention at all (→ `null`, never `true`).
- **A departed person who never practised is not resurrected** as an empty row.
- **Ordering:** active-with-practice by volume, then silent actives, then the departed — last, no
  matter how many attempts their history holds.
- **`weakestStageKey: null` reads «нет данных»**, never «слаб везде».
- **The «уже не работает» mark is withheld entirely** when neither service could check the roster,
  and the amber `role="status"` strip says so.
- **`unattributedAttemptCount > 0` always footnotes**, even for three attempts, and the headline
  attempt total includes them — folding an unknown into a known bucket is a claim nobody can check.
- **No leaderboard.** A team dashboard is where the old product put one; the render test fails on
  `xp`, `опыт`, `стрик`, `streak`, `лига`, `league` anywhere in the DOM.

### Manual — O1

Preconditions: a `TenancyAdmin` whose organization has at least one manager with attempts.

| Scenario | Expect |
|---|---|
| Open `/org` on a slow connection | header skeleton, two card placeholders, an 8-row grid — never an empty table |
| Stop learning-service, open `/org` | one `ErrorState` with «Повторить» — map and panel fail together, because they share one window |
| Switch the window 30 → 90 → 180 | both the map and the suggestion panel re-read; the «Данные с …» line moves with them, and they never disagree |
| Switch «по этапам» → «по навыкам» | columns change, no network request is issued |
| A brand-new organization with no attempts | «Пока никто из команды не решал упражнения» + «Создать задание» → `/org/assignments/new`; when the roster is readable the copy also names the headcount |
| An organization where nothing is failing | «Ни один этап воронки не проваливается …» quoting the three thresholds **from the response** |
| A stage suppressed as `run_in_progress` | grey card, «Уже идёт генерация», «открыть прогон» → `/org/content/generation/{jobId}` |
| A stage suppressed as `dismissed` | «Отложено вами до …» + «Вернуть предложение»; pressing it brings the offer back |
| Press «Не сейчас» | a modal with an optional note; confirming replaces the panel from the mutation's own response, no second read |
| Press «Сгенерировать упражнения» | navigates to `/org/content/generation/{jobId}`; pressing twice lands on the same run, never a second one |
| Press it after the window has moved (409) | the card shows «Окно сдвинулось…», the screen does not navigate |
| Stop identity-service, keep learning-service up | the map still renders; roster marks fall back to whatever `skill-map` knew — no error, no red banner |
| Click a manager's name | `/org/dialogs?userId={userId}` |
| Click a cell | nothing happens — there is no per-skill attempt filter in the API to click through to |

---

## Slice 4 — O8 «Профиль компании» (`/org/profile`)

The interview, the draft preview and the full form. What this screen writes ends up in every lesson
the organization sees (`{{organization.*}}` substitution, docs/CONTENT_PARAMETERIZATION.md) and in
two prompts at once — the AI persona's and the grader's.

### Automated (vitest)

| File | Covers |
|---|---|
| `__tests__/orgProfileInterview.test.ts` | which questions a round shows, the «осталось ещё N» count, the editor-per-gap-code map, per-answer validation, and that one answer patches exactly one field |
| `__tests__/orgProfileForm.test.ts` | the full form's load/save round trip, blank-row dropping, duplicate-term and orphaned-answer validation, `findRemovedBannedClaims`, stage reordering |
| `__tests__/orgProfileDraft.test.ts` | grouping of the four merge decisions, accept-field toggling, the «nothing to apply» case, and the `sessionStorage` handoff from the 40.27 checkpoint |
| `__tests__/orgProfileService.test.ts` | 404-as-«ещё нет», the `limit=3` default, which verb goes to which route, and the sentence a 403 turns into |
| `__tests__/OrgProfileBannedClaims.test.tsx` | the two-confirmation gate on removing a banned claim, end to end through the real form |

The behaviours worth naming, because they are the ones that break quietly:

- **A 404 from `GET /organizations/profile` is the first-run case, not an error.** The service turns
  it into `null`; anything else still throws. An organization that has never saved a profile must
  see the interview, never «Что-то пошло не так».
- **One answer is one `PATCH` naming one field.** The test asserts the patch body has exactly one
  key. A read-modify-write `PUT` would silently discard whatever a colleague answered meanwhile, and
  the multi-person case is the expected one here.
- **`banned_claims` and `glossary` are hidden while any blocking gap is open**, and neither ever
  holds readiness hostage. Their honest answer may be «таких нет», which the schema cannot record —
  hence the «Таких нет» button, which hides the question for that sitting only.
- **The readiness bar has two states, never a percentage.** It reads
  `isReadyForParameterization`; «5 из 7» would call a finished profile unfinished and an unusable one
  nearly done.
- **A gap code with no editor is dropped, not rendered blank** — the same closed-vocabulary rule the
  backend applies to unknown codes.
- **Conflict checkboxes in the draft preview start unticked**, `unchanged` proposals are not rendered
  at all, and `banned_claims` cannot be ticked: `toggleAcceptedField` refuses every field outside
  `OrganizationProfileFields.Overwritable`, so the screen can never promise a replacement the server
  would ignore — or a deletion it must never perform.
- **Removing a banned claim takes two confirmations.** The row's delete button opens a dialog naming
  the claim; removing it still writes nothing. Pressing «Сохранить» then opens a second dialog
  listing every claim that would stop being forbidden. Only «Снять и сохранить» issues the `PUT`. A
  save that removes nothing asks nothing.
- **Russian agreement is computed, not concatenated.** «1 вопрос / 2 вопроса / 5 вопросов /
  11 вопросов / 21 вопрос» are all pinned.

### Manual — O8

Preconditions: a `TenancyAdmin`. A plain `Manager` cannot reach `/org/*` at all; a member who can
read the panel but is not an org administrator gets 403 on every save (see the last row).

| Scenario | Expect |
|---|---|
| Open `/org/profile` in an organization that never saved a profile | grey bar «Готов к подстановке: нет», three questions, «Осталось ещё 4 вопроса». No error anywhere, though `GET /organizations/profile` returned 404 |
| Slow connection | header + two card skeletons, never an empty form |
| Stop organization-service | one `ErrorState` with «Повторить»; profile and gaps fail together |
| Answer the first question | the button reads «Сохраняем…», then the question disappears and the next one arrives; only `PATCH` was sent, with one field in the body |
| Have a colleague answer a different question in another tab, then answer yours | both answers survive — this is what the `PATCH` contract buys |
| Break the network and press «Ответить» | the error appears under **that** question; the other two keep whatever was typed in them |
| Answer «Возражения» with two entries | refused client-side — the server counts three as the threshold and would return the same gap |
| Answer «Этапы скрипта» with two stages | same refusal, same reason |
| Answer everything blocking | the bar turns green, «Уроки говорят про ваш продукт», remaining optional questions are listed as non-blocking |
| Fill in all seven | «Профиль заполнен» + a read-only summary; the interview does not render |
| «Таких нет» on the banned-claims question | the question goes away for this sitting; a reload brings it back, because nothing was written |
| «Заполнить по материалам» | a dialog asking for a title and pasted text (≥200 characters), with the side effect stated: the same run also produces a hidden draft lesson. Confirming starts `POST /admin/content-generation` and navigates to `/org/content/generation/{jobId}` |
| Return from the checkpoint with «Заполнить профиль компании из этой структуры» | the preview opens: «Заполнится», «Дополнится + N», «Расхождение — решать вам». Nothing is pre-ticked |
| Apply the preview without ticking anything | blanks are filled, lists grow, every existing value survives, and the interview reappears **without a second request** |
| Reload while the preview is open | the draft is gone (the slot is read once); the interview is shown |
| «Показать все поля профиля» | the seven sections, the «сохраняется всё разом» warning above them, «Изменения доходят … за несколько секунд» below |
| Empty a field on the full form and save | it is cleared — this is the only place in the product that can |
| Delete a banned claim, then save | two dialogs, in that order; cancelling either one changes nothing on the server |
| Save as a member who is not an org administrator | «Изменять профиль компании может только администратор организации» — a 403 is a role, not a fault to retry |
| Look for XP, streaks or leagues | none, on any of the three modes |

---

## Slice 1 — Задания (O2 `/org/assignments`, O3 `/org/assignments/new`, O4 `/org/assignments/[assignmentId]`)

The three screens behind [ADMIN_UI_DESIGN.md §O2–O4](../TENANCY/ADMIN_UI_DESIGN.md) and the
semantics of [TENANCY/ASSIGNMENTS.md](../TENANCY/ASSIGNMENTS.md): completion is a **quality
threshold**, `failed_threshold` is a visible fifth state, and the audience is a rule rather than a
list of names.

### Endpoints each screen calls

| Screen | Calls |
|---|---|
| O2 | `GET /admin/assignments`, `DELETE /admin/assignments/{id}` (drafts only) |
| O3 | `POST /admin/assignments`, `POST /admin/assignments/{id}/activate`, `GET /admin/lessons`, `GET /admin/lessons/{lessonId}/versions`, `GET /dialog/bundles`, `GET /dialog/bundles/{bundleId}/modes`, `GET /reference?search=`, `GET /admin/team/skill-map` (via `useTeamMemberNames`) |
| O4 | `GET /admin/assignments/{id}/dashboard`, `GET /admin/assignments/{id}`, `PUT /admin/assignments/{id}`, `POST /admin/assignments/{id}/remind?scope=`, `POST /admin/assignments/{id}/close`, `POST /admin/assignments/{id}/activate` (draft), `GET /admin/assignments/{id}/progress` (error fallback only) |

### Automated (vitest)

| File | Covers |
|---|---|
| `__tests__/orgAssignmentsLogic.test.ts` | the §1.4 dictionary, completion-rule drafting and validation, audience-rule building, funnel maths, deadline wording, repeat-schedule bounds, content-draft ordering, and the 503/409/400 failure wording |
| `__tests__/OrgAssignmentsComponents.test.tsx` | `AssignmentFunnelBar`, `AssignmentFunnel`, `RemindDialog`, `CompletionRuleEditor` |

The behaviours worth naming, because they are the ones that break quietly:

- **`notStarted` is derived, not read.** `AssignmentSummaryDto` has no such field, so the list row
  computes `assignedCount − startedCount` and «в работе» as the rest — both clamped at zero so an
  inconsistent read never draws a negative segment.
- **«▲ N ниже порога» appears only when N > 0.** It is the one line of the list worth opening an
  assignment for; a zero would bury it.
- **The dashboard funnel has five stages.** `failed_threshold` is a column, never a slice of
  «выполнили».
- **Neither threshold radio starts selected**, and a rule whose content the assignment does not
  carry cannot be selected at all — `activate` answers 409 for exactly that pairing.
- **The «одна из двух половин» warning renders only when both `lesson_version` and
  `dialog_scenario` are present.**
- **An unknown `completionRule.kind` reads as «no rule»** in the editor and renders nothing in the
  sentence — never as a guess.
- **An unknown `repeatSchedule.kind` is never rewritten**; the editor falls back to `[7, 21]` and
  the list prints no offsets.
- **503 is worded as «nothing was written, press it again»** on issue, save and remind — it is not a
  generic failure.
- **`?action=remind` opens the dialog and sends nothing.** Only the button sends.
- **`notifiedCount` is reported as an intention** («Напоминание отправлено: 3 человека»), and the
  dialog says a repeat within the hour will not arrive.
- **No XP, no streaks, no leagues** anywhere in the slice.

### Manual — O2 `/org/assignments`

| Scenario | Expect |
|---|---|
| Open with no assignments at all | «Заданий пока нет» + «Создать первое задание» |
| Filter to a status with nothing in it | «В этом статусе ничего нет» + «Показать все» |
| Slow connection | five skeleton rows, never an empty table |
| Stop learning-service | one `ErrorState` with «Повторить» |
| A wave row | title carries «· волна 2» and a third line «↳ повтор задания «…»» when the origin is in the same array, «↳ повтор» when it is not |
| An assignment whose deadline has passed while still active | the cell reads «прошёл 11 авг.» in amber, not red |
| A draft row | «Удалить» appears; the first click arms it («Точно удалить?»), the second deletes; row click still opens the card |
| An issued row | no «Удалить» at all — `DELETE` answers 409 for anything ever issued |
| Click any row | `/org/assignments/{id}` |

### Manual — O3 `/org/assignments/new`

| Scenario | Expect |
|---|---|
| Open the screen | «Выдать команде» is disabled and the reason is printed next to it |
| Pick a lesson with no published version | the version button is replaced by «у урока нет опубликованной версии» and a link into the lesson editor |
| Pick the same lesson version twice | the second attempt is marked «✓ уже добавлено» and cannot be clicked — a duplicate `(kind, reference)` is a 400 |
| Add a conversation | the persona fields appear inside that row and nowhere else |
| Reorder by dragging, or with the ↑/↓ buttons | positions renumber; no `orderIndex` field is ever typed |
| Choose «Разговоры» with no conversation in the assignment | the radio is disabled, with the reason under it |
| Add both exercises and a conversation | the amber «порог измеряет только …» warning appears |
| Set the required count to 21 or the score to 0 | refused client-side with the server's own bounds quoted |
| Choose «Выбрать людей» | the list is the skill-map roster, with «Здесь только те, кто уже что-то решал» under it |
| Enable repeats and type 21 then 7 | «по возрастанию» refusal before anything is sent |
| «Сохранить черновик» | one `POST`, then the card |
| «Выдать команде» | «Создаём…» then «Выдаём…», then the card with the funnel |
| Press «Выдать команде» again after a failure | no second draft is created — the id from the first `POST` is reused |
| Stop identity-service, then «Выдать команде» | «Задание сохранено черновиком, нажмите «Выдать» ещё раз» |

### Manual — O4 `/org/assignments/[assignmentId]`

| Scenario | Expect |
|---|---|
| Open a draft | no funnel and no table — «Задание ещё не выдано» + «Выдать команде» |
| Open an assignment issued an hour ago | the funnel reads «Выдано 12 · пока никто не начал», which is not an error |
| Open a closed assignment | read-only: no «Закрыть», no reminder button |
| A person with no `UserReplicas` row | «Без имени · 3f2a1b9c», never a placeholder name |
| A person who left, with the roster readable | a «†» and the footnote under the table |
| Stop identity-service (`rosterKnown: false`) | «Не удалось проверить, кто ещё работает в компании», no «†» anywhere, the reminder button still enabled |
| Break the dashboard entirely | `ErrorState` + «Показать сырые строки», which renders `GET …/progress` — ids, no names |
| A repeat series | the wave tabs appear only from the second wave on; clicking another wave navigates to its own card |
| Open `/admin/assignments/{id}?action=remind&scope=not_started` from the deadline digest | slice 0 redirects to `/org/assignments/{id}?…`; the dialog is **already open**, preset to «тем, кто не начал», and **nothing has been sent** |
| Inside the dialog | the recipients are listed by name, and the counts match the funnel |
| Send it | «Напоминание отправлено: N человек»; stopping identity-service instead gives «Никто не получил напоминание» |
| Expand «Содержание и настройки» on an issued assignment | content and threshold are shown but frozen, with the sentence explaining why; title, goal, audience, dates and repeats stay editable |
| Add a new hire to an issued assignment and save | rows are appended, nobody is removed |
| Look for XP, streaks or leagues | none |

### What the backend cannot serve, and what the screens do instead

| Design asks for | Reality | Degraded behaviour |
|---|---|---|
| «6 человек» under a `users` draft in the list | `AssignmentSummaryDto` carries `audienceKind` only — no user ids, no count — and a draft has no progress rows to borrow one from | «выбранные люди» until the assignment is issued, then the resolved `assignedCount` |
| «повтор +7, +21» under a list row | the summary carries `hasRepeatSchedule: bool` and no offsets | «с повторами»; the exact offsets appear on O4, which reads the full assignment |
| Titles of the content items on O4 | `AssignmentDto.content` is `(kind, reference, orderIndex, persona)` — the learner-facing `ActiveAssignmentDto` resolves titles, the admin one does not | the kind plus the raw reference (a dialog mode key is already readable; a lesson version is a uuid) |
| A people picker for the audience | identity-service has no `GET /memberships` (§6.1) | `GET /admin/team/skill-map` members, with the caveat printed under the list |
| Per-status counts on the filter chips | `GET /admin/assignments` has no counts endpoint and does not paginate | the whole array is read once and both the counts and the filtering are done on the client |

---

## Slice 3 — Разговоры и споры (O5, O6, O7)

Screens: `/org/dialogs`, `/org/dialogs/[sessionId]`, `/org/reviews`.
Code: `src/frontend/features/org-dialogs/**`, `src/frontend/app/(org)/org/{dialogs,reviews}/**`.

Endpoints, and nothing else:

| Screen | Reads | Writes |
|---|---|---|
| O5 | `GET /admin/dialog-sessions?userId=&modeId=&maxScore=&limit=` (ai-service), `GET /admin/team/skill-map` for the names | — |
| O6 | `GET /admin/dialog-sessions/{sessionId}` (ai-service), `GET /admin/dialog-reviews?sessionId=` (learning-service) | `POST /admin/dialog-reviews` |
| O7 | `GET /admin/dialog-reviews` (unfiltered — see below) | `POST /admin/dialog-reviews/{noteId}/resolve` |

### Automated (vitest)

| File | Covers |
|---|---|
| `__tests__/orgDialogSessions.test.ts` | the two grade scales and the conversion between them, the `GET /admin/dialog-sessions` query string, the 25→100 paging arithmetic, «вчера, 14:20»/«18 авг», and the «Без имени · 8 символов» fallback |
| `__tests__/orgTranscriptSelection.test.ts` | click / shift+click range selection on the server's message `index`, and the quoted text it builds |
| `__tests__/orgDisputeVerdict.test.ts` | the verdict form's rules — a rejection needs words, an agreement does not, a corrected score is 0–100 and only on an agreement |
| `__tests__/orgReviewNotes.test.ts` | one note read as a two-sided thread, «это вы» on either side, the three queues, and `?note=` pinning |

The behaviours worth naming, because they are the ones that break quietly:

- **One conversation, two grade scales.** ai-service grades 0–10 and its `maxScore` filter compares
  against that; learning-service stores the same grade ×10 and every dispute (`disputedScore`,
  `adjustedScore`) argues about *that* number. The panel shows 0–100 everywhere and converts on the
  ai-service edge, so «не выше 60» on screen is `maxScore=6` on the wire.
- **The grade ceiling is a select of whole tens, not a number box.** A box would accept 65 and
  quietly search for 60.
- **The message `index` is the server's**, never a position in the array — the test transcript says
  the same sentence at index 5 and index 7 and asserts that quoting 7 quotes 7.
- **`quotedText` is sent whole**, because the server copies it into the row: the note has to still
  read after Mongo has trimmed the session.
- **An ungraded conversation cannot be commented on** — the composer is replaced by the sentence
  saying so, before the server's 400.
- **A rejection with no reason is refused on screen**, in the server's own words, and a corrected
  score is never sent alongside one.
- **«Справедливая оценка» always carries its caption** — the number is recorded and never applied.
- **`?note={id}` pins that note above whichever tab is open** rather than switching tabs: a verdict
  moves its note between queues the moment it is given, and a tab that followed it would move the
  reader mid-sentence. A stale or foreign id opens the queue, never an error.
- **A dead ai-service leaves O6 half-working**, not broken: the transcript column shows the error
  and the already-sent notes beside it keep rendering. Two services, two failures.
- **No XP, no streaks, no leagues.**

### Manual — O5 `/org/dialogs`

Preconditions: a `TenancyAdmin`, and a team with at least one graded conversation.

| Scenario | Expect |
|---|---|
| Open the screen | the grade ceiling is already 60; the list is the conversations that went badly |
| A brand-new organization | «Команда ещё не провела ни одного оценённого разговора» — appears only with the filters cleared |
| Filter down to nothing | «Под фильтр не попал ни один разговор» **plus** «Показать все разговоры», which clears every filter |
| A manager the heat map has never seen | «Без имени · 3f2a1b9c», never a placeholder name |
| A conversation held for an assignment | a «по заданию» chip linking to O4; the rest of the row still opens the transcript |
| «Показать ещё 25» four times | the button is replaced by «Показаны первые 100. Сузьте фильтр…» |
| Stop ai-service, keep learning-service | «Не удалось получить разговоры» here, while `/org` still draws its heat map |

### Manual — O6 `/org/dialogs/[sessionId]`

| Scenario | Expect |
|---|---|
| Click a reply | it highlights; clicking it again clears it |
| Shift+click a later reply | the range covers both, and «Выделено: реплики 5–6» appears |
| Send with no comment, or with nothing selected | «Отправить менеджеру» stays disabled, with the sentence saying what to do first |
| Send a note | it appears under «Уже отправлено» and the selection clears |
| Open a conversation with no grade | the composer is replaced by «Разговор не оценён — прокомментировать его нельзя» |
| Open a session id from another company | «Разговор не найден», not an error banner |
| Stop ai-service | the left column errors, the right column still lists what was sent |
| A note the manager has read | its thread shows a second turn «Иванов А. · прочитал заметку» |

### Manual — O7 `/org/reviews`

| Scenario | Expect |
|---|---|
| Open with an open dispute | the manager's words first, on their side of the thread; the verdict controls below them |
| «Оставить оценку» with an empty reason | «Вынести решение» disabled, «Оценка остаётся в силе, потому что…» under the field |
| «Согласиться» | no reason required; «Справедливая оценка» appears with its caption about not changing anything |
| Type 101 into «Справедливая оценка» | «Оценка — целое число от 0 до 100.» |
| Give a verdict | the card re-renders as a two-turn thread and the sidebar badge drops by one |
| A dispute another administrator ruled on | their name on the verdict turn, no «это вы» chip |
| Open `/admin/dialog-reviews?note={noteId}` from the notification | slice 0 redirects to `/org/reviews?note={noteId}`; that card is outlined, its quote unfolded, and the page is scrolled to it |
| The same link after the dispute was already ruled on | the card is still pinned and shows the verdict |
| «Мои заметки» | the coaching notes the organization sent, each with its read/unread status |
| Empty «Открытые» | «Открытых споров нет» + how a dispute gets here |

### What the backend cannot serve, and what the screens do instead

| Design asks for | Reality | Degraded behaviour |
|---|---|---|
| «Оценка не выше 60» filtering on the same numbers the cards show | `GET /admin/dialog-sessions?maxScore=` compares against ai-service's 0–10 grade, while O7's `disputedScore`/`adjustedScore` are 0–100 | the panel shows 0–100 and divides the ceiling by ten on the way out; the ceiling is a select of whole tens because nothing between them exists |
| «Иванов А. оспаривает 41 балл» | the grade reaching learning-service is always a multiple of ten (`score × 10`), so 41 cannot occur | «оспаривает 40 баллов» |
| «Жёсткий закупщик» on a dispute card | `DialogReviewNoteDto` carries `dialogModeKey` and no title, and no endpoint names an organization's dialog modes | the raw key (`tough-buyer`) — not prettified into something the backend never said |
| A scenario selector on O5 | there is no route listing the dialog modes one organization uses | distinct `modeId`/`modeTitle` over the rows already returned, keeping the applied one so the control never offers only its own value |
| Manager names on O5/O6 | `AdminDialogSessionSummaryDto` carries `userId` only — ai-service holds no user replica | `GET /admin/team/skill-map` through `useTeamMemberNames`, and «Без имени · {8 символов id}» for anybody it does not know |
| Three filtered reads for O7's three tabs | `GET /admin/dialog-reviews` does not paginate, and there is no by-id route for `?note=` | one unfiltered read; tabs, counts and the deep link are resolved from it |
| «Открытые 2» counting only what is addressed to *this* administrator | the queue is the organization's; the endpoint has no author filter, and «Мои заметки» is `kind=coaching_note`, which is every administrator's | both tabs are the organization's, and each turn of a thread is labelled «это вы» when it is the reader's |

---

## Slice 5 — O9/O10/O11 «Конвейер генерации контента» (`/org/content`, `/org/content/generation`)

The hub page, the list of runs, and the one screen that serves all six states of a run. Semantics:
[docs/CONTENT_PIPELINE.md](../CONTENT_PIPELINE.md). Design:
[docs/TENANCY/ADMIN_UI_DESIGN.md → O9–O11](../TENANCY/ADMIN_UI_DESIGN.md).

**Endpoints, per screen.**

| Screen | Calls |
|---|---|
| O9 `/org/content` | `GET /admin/content-generation`, `GET /admin/content/adaptations`, `GET /admin/content/overrides` — counters only, nothing else read |
| O10 `/org/content/generation` | `GET /admin/content-generation?status=`, `POST /admin/content-generation` |
| O11 `/org/content/generation/{jobId}` | `GET /admin/content-generation/{jobId}` (polled), `PUT …/structure`, `POST …/material`, `POST …/approve`, `POST …/retry`; plus `GET /admin/lessons` + `PUT /admin/lessons/{id}` behind «Показать команде» |

**Two things the design asked for that the backend cannot serve, and what happens instead.**

- **`POST /organizations/profile/draft` is not called from O11.** «Заполнить профиль компании из
  этой структуры» writes the reviewed structure into O8's own `sessionStorage` slot
  (`PROFILE_DRAFT_HANDOFF_STORAGE_KEY`) and navigates to `/org/profile`, which previews the draft
  against the live profile and applies only what a person ticks. Calling the preview route from here
  would render the four merge decisions on a screen that cannot apply them.
- **There is no `GET /admin/lessons/{id}`, and the list DTO carries no `isArchived`.** So
  «Показать команде» reads `GET /admin/lessons`, finds the produced row for its required `title` and
  `orderInTopic`, and then `PUT`s `isArchived: false`. The consequence is visible: **the screen
  cannot show whether the lesson is already visible to the team** — it can only perform the
  un-archive and confirm it for the current sitting. The action is idempotent, so pressing it twice
  costs nothing.
- **O3 does not yet read `contentGenerationJobId`.** «Создать задание по этому уроку» links to
  `/org/assignments/new?contentGenerationJobId={jobId}`; the parameter is inert until slice 1 reads
  it. The backend side is real — `POST /admin/assignments` derives `sourceType`/`sourceRef` from the
  run and ignores a client's — so nothing here needs changing when it does.

### Automated (vitest, from `src/frontend`)

```
npx tsc --noEmit
npx vitest run
```

| File | Covers |
|---|---|
| `__tests__/orgContentGenerationState.test.ts` | the job state machine: six statuses → five layouts, the checkpoint gate, the polling rule, the refusal's shape, the 409 body, and the start form's two validations |
| `__tests__/orgContentGenerationStructure.test.ts` | the checkpoint document — draft ↔ payload, blank-row dropping, the four caps, autosave change detection — and O9's counter copy |
| `__tests__/OrgContentGenerationScreens.test.tsx` | `InsufficiencyPanel`, `RunProgressPanel`, `StructureEditor`, `CompletedRunPanel`, `FailedRunPanel`, `ContentQueueCard` rendered with `@testing-library/react` |

The behaviours worth naming, because they are the ones that break quietly:

- **There is no «сгенерировать всё равно», and the test proves the negative.** The rendered refusal
  is scanned for «всё равно», «принудительно», «игнорировать» and «пропустить проверку», and its
  button list is asserted to be exactly «Добавить» + «Открыть структуру». The checkpoint and the
  sufficiency threshold are the two blocks that stop money being spent on unusable material; one
  bypass button cancels both, and the backend has no route behind it either.
- **Approval is offered in exactly one state.** `canApproveStructure` is filtered over all six
  statuses and must return `["awaiting_review"]` — in particular **not** `insufficient`. The
  threshold is arguable, not waivable.
- **The refusal is a list of bullets and never a paragraph**, and **the model's `note` never
  reaches the DOM** — it is a developer's diagnostic, and the customer's text is the gaps.
- **A gap with neither a known code nor a sentence is dropped**, not rendered as an empty bullet —
  the same closed-vocabulary rule the backend applies to a code a model invented. A blank message on
  a *known* code falls back to that code's own sentence.
- **«Открыть структуру» appears only at `stage: "structure"`.** At `stage: "material"` nothing was
  extracted, so offering an editor over `null` would be offering to invent the reading. Pinned
  including the case where a structure somehow survives on a material-stage refusal.
- **Polling is three seconds while `structuring`/`generating`, `false` in every other state, and
  `false` behind a hidden tab** — mid-generation included. It also does not poll before the first
  response has said what the status is.
- **Thin material is not a form error.** `validateStartMaterial("три слайда")` returns `null`; only
  emptiness and the 60 000-character ceiling are refused client-side. Refusing thin material here
  would replace an answerable run with a red field and teach nobody anything.
- **A blank field is sent as `null`, never `""`.** A gap stays a gap: the generation prompt reads
  the two differently, and 40.29's promotion would copy an empty string over a value a human typed.
- **The autosave issues no `PUT` for a change that is not one.** An added-but-empty row and trailing
  whitespace both compare equal, so «сохранено 14:22» only ever appears after a real write.
- **The four caps are the server's own** (10 / 12 / 30 / 20, 2000 characters per value) and the same
  numbers drive both the «7 из 10» counter and the disabled «+ добавить». An empty list reads
  «Запрещённые обещания (0 из 20) — пусто» rather than being hidden.
- **A queue whose counter failed to load says so.** O9 shows «Не удалось прочитать очередь» instead
  of a zero it did not measure — a card reading «0 ждёт проверки» sends somebody away from work that
  is there. Each of the three counters fails independently.
- **A completed run with `producedExerciseCount: 0` does not offer «Показать команде»** and says out
  loud that nothing passed validation, and the finished layout offers no per-item accept/reject —
  that is O13 and a separate life.
- **No gamification.** The four layouts are rendered together and the DOM is asserted to contain no
  `xp`, `опыт`, `стрик`, `streak`, `лига`, `league`.

### Manual — O9, O10, O11

Preconditions: a `TenancyAdmin`, learning-service and ai-service up, and a way to reach the
`insufficient` and `failed` states (three slides of unrelated text for the first; stopping
ai-service mid-run for the second).

| Scenario | Expect |
|---|---|
| Open `/org/content` in a fresh organization | three cards, each explaining its section — no «0» anywhere |
| Stop learning-service, open `/org/content` | the cards still render, with «Не удалось прочитать очередь» in place of numbers; no red error page |
| «Сделать урок из материалов» on O9 | lands on `/org/content/generation?new=1` with the form already open |
| Submit the form with an empty textarea | refused on the client, with the sentence naming what to paste; no request is sent |
| Paste three slides of a cake recipe | the run is **created**, not refused by the form; the screen lands on O11 showing «Похоже, этот материал не про продажи» |
| Paste a real deck | O11 shows «Разбираем материал…» and says the page may be closed |
| Close the tab during structuring, come back a minute later | the run is at the checkpoint; the list shows «Ждёт проверки» |
| Watch the network tab while `structuring` | one `GET` every three seconds; switch to another browser tab → the polling stops; switch back → it resumes |
| Watch the network tab at the checkpoint | no polling at all |
| Edit the product field, wait two seconds | one `PUT …/structure`; the line reads «Сохранено HH:MM» |
| Add an empty objection row and wait | **no** request is sent |
| Add ten objections | «+ добавить» goes grey and the counter reads «10 из 10» |
| Delete everything and press «Сгенерировать упражнения» | 409 → the screen becomes the refusal layout with the gap list, without a page reload |
| Look for any way to generate anyway | there is none, on any of the six states |
| On a refused run, paste the objections list into «Добавить материал» | the run returns to «Разбираем материал…», then to the checkpoint, and the structure it already had is still there plus what the added text produced |
| On a refused run at `stage: "structure"`, press «Открыть структуру», type four objections | the editor saves; the run returns to «Ждёт проверки» on its own |
| On a refused run at `stage: "material"` | «Открыть структуру» is absent — there is nothing to open |
| «Заполнить профиль компании из этой структуры» | `/org/profile` opens the draft preview with nothing pre-ticked; the run itself is unchanged, and no profile write happened |
| Approve a good structure | «Собираем упражнения…», then the finished layout with the exercise count |
| «Показать команде» | the lesson leaves the archive; the line becomes «Урок показан команде» |
| «Открыть урок» | `/org/content/lessons/{producedLessonId}` — a 404 until slice 7 lands |
| Stop ai-service and let a run burn its three attempts | the failed layout, the recorded reason, and «Повторить» resuming the half that failed |
| Open `/org/content/generation/{a-random-guid}` | «Прогон не найден» with a way back — not an error banner |
| Press «Сделать контент по этому провалу» on `/org` | lands on this same O11, with «с дашборда» in the header and the composed material readable under «Исходный материал» |
| Filter the list by «Материала не хватает» | each row shows its first gap under the title |

---

## Slice 6 — Пакетная адаптация и ИИ-ревью (O12, O13)

Screens: `/org/content/adaptations`, `/org/content/adaptations/[jobId]`.
Code: `src/frontend/features/org-content-adaptation/**`,
`src/frontend/app/(org)/org/content/adaptations/**`.
The semantics these screens are drawn against: [CONTENT_PIPELINE.md §6a](../CONTENT_PIPELINE.md).

Endpoints, and nothing else:

| Screen | Reads | Writes |
|---|---|---|
| O12 | `GET /admin/content/adaptations` (unfiltered — one read, two tabs), `GET /skills/stages` for the stage names | `POST /admin/content/adaptations {mode, stageKey}` |
| O13 | `GET /admin/content/adaptations/{jobId}` (polled every 5 s while `preparing`), `GET …/items/{itemId}`, `GET /skills/stages` | `POST …/items/{itemId}/accept`, `POST …/items/{itemId}/reject`, `POST …/retry` |

**There is no bulk verb on either screen, and its absence is the feature.** The backend has no route
that answers more than one item, and the design refuses to grow one (§7): a batch is worth running
only because a person reads each rewrite before it becomes their team's content.

### Automated (vitest)

| File | Covers |
|---|---|
| `__tests__/orgContentAdaptationLogic.test.ts` | the proposal state machine, queue order, the seven review codes, the two status dictionaries, and the refusals coming back from `POST /admin/content/adaptations` and from accept/reject |
| `__tests__/OrgContentAdaptationComponents.test.tsx` | `ProposalDiffView`, `FindingList`, `ProposalQueueList`, `ProposalDetailPanel` — including that no rendering of the queue contains a bulk-apply control |

The behaviours worth naming, because they are the ones that break quietly:

- **Three separate reasons «Принять» can be impossible, and they never collapse into one.** A review
  finding has nothing to apply *ever*, a stale rewrite would be refused with 409, and an answered
  item is simply done. Each has its own sentence; a single greyed-out button would leave a person
  guessing which of the three they are looking at.
- **In `quality_review` the «Принять» button does not exist at all** — not disabled, absent — and the
  two controls that replace it are «Открыть упражнение» and «Переписать этот этап под нас».
- **The publishing caveat is printed under every accept.** Accepting writes the exercise draft; the
  team meets it only after somebody publishes a lesson version.
- **The diff is never computed on the client.** `changes[]` is rendered as it arrived; when the
  server sends a proposal with an empty change list, the screen says so and points at the editor
  instead of comparing two documents itself.
- **A blocking finding rises to the top of its own lesson and no further** — the reading order of the
  rest of the queue is what makes it answerable.
- **`awaitingReviewCount`, never `pendingCount`, is the headline number.** A batch is not done when
  the model finishes.
- **`… ещё N` collapses a long change list** rather than dumping forty leaves into the panel.
- **A code outside the closed vocabulary of seven prints as the code**, never blank and never mapped
  onto the nearest known label.
- **`retry` is rendered only when `failedCount > 0`**, because it answers 409 when nothing failed.

### Manual — O12 `/org/content/adaptations`

| Scenario | Expect |
|---|---|
| Open with no batches at all | the tab's own explanation of what the section does — «Пакетов правки ещё нет…» — not «0» |
| Switch to «Проверить, что написали руками» | a different explanation, about what the review reports and that fixing is still yours |
| A batch with nine unanswered proposals | the tab badge counts the batch, and the row reads `9 / 23` under «Ждут ответа» |
| A batch the model has finished and nobody has answered | status «Ждёт вашего ответа», not «Готово» |
| «Переписать этап под свой продукт» → a stage with 412 exercises | «В этапе 412 упражнений — это дорого и это очередь, которую никто не разберёт. Выберите этап поуже.» as advice, and no English anywhere |
| The same stage twice | «По этому этапу уже идёт пакет» plus a working link to that batch |
| An empty stage | «В этом этапе нет упражнений — переписывать нечего.» |
| Stop learning-service | `ErrorState` with a retry, never an empty table pretending there are no batches |

### Manual — O13 `/org/content/adaptations/[jobId]`

| Scenario | Expect |
|---|---|
| Open a batch the sweep is still working on | the queue renders *and* a progress bar «Готовим предложения 12 / 23» above it, with «страницу можно закрыть»; it advances every five seconds without a reload |
| Leave the tab and come back | polling resumed; no burst of catch-up requests |
| Open a `tone_rewrite` item | the model's sentence first, the changed leaves under it, `path` in the monospace face |
| Item with seven changed leaves | four shown, «… ещё 3» reveals the rest |
| Press «Принять» | the item resolves and the panel moves to the next one still waiting |
| Press «Принять» on the last unanswered item | it resolves and «Следующее →» is gone rather than pointing at itself |
| An item whose exercise was edited elsewhere (`isStale`) | «Принять» disabled, and the sentence about re-running the batch above it |
| Accept anyway through the API and refresh | 409 handled as «Запустите пакет заново», never as a merge attempt |
| Open a `quality_review` item | findings only: severity chip, the short title, the server's Russian sentence, and the quoted fragment in monospace |
| A review item with a blocking finding | `⚠` in the list, first inside its lesson, and the blocking finding first in the panel |
| A review item the model had no complaints about | «Замечаний нет» phrased as the expected answer |
| Look for «Принять» in review mode | it is not there; «Открыть упражнение» and «Переписать этот этап под нас» are |
| «Переписать этот этап под нас» | the start dialog opens with this batch's stage already selected and the mode fixed to the rewrite |
| A batch with three failed items | the red panel with «Повторить неудавшиеся»; on a batch with none, no button at all |
| The last answer in the batch | «Все предложения разобраны: принято 11, отклонено 2, без изменений 1» plus links to the lessons that now need publishing |
| A batch where the model changed nothing anywhere | «Разбирать нечего: модель не предложила ни одной правки.» — not three zeroes |
| An unknown job id | «Пакет не найден», not an empty two-column layout |
| Look for «Применить всё», XP, streaks or leagues | none of them exist |

### What the backend cannot serve, and what the screens do instead

| Design asks for | Reality | Degraded behaviour |
|---|---|---|
| «В этапе 412 упражнений…» as a Russian sentence | the ceiling refusal is `ContentAdaptationValidationException` — English developer prose in `{message}`, with no machine-readable payload the way `POST /admin/content-generation/{id}/approve` has one | the count is recovered from the sentence with a pattern, the Russian advice is written on the client, and the English is never shown; a refusal shape the client does not recognise degrades to a generic Russian sentence |
| A link to the batch a 409 is refusing in favour of | the conflict message names the job id only inside its English prose | the live batch is found in the list already read (same `mode` + `stageKey`, status `preparing`/`awaiting_review`); if the list has not caught up, the message is shown without a link rather than with a guessed one |
| «Открыть упражнение» → the exercise inside the lesson editor | O19 (`/org/content/lessons/[lessonId]`, slice 7) is the editor, and no route addresses a single exercise inside it | the link points at the lesson; there is no exercise anchor, because inventing a query parameter would be inventing a contract |
| «Ссылка на публикацию затронутых уроков» | nothing publishes from this block, and there is no «publish these lessons» route | the lessons with accepted items are listed as links to O19, where publishing lives |
| Per-status counts on the two tabs | `GET /admin/content/adaptations` does not paginate and has no counts endpoint | one unfiltered read; both tabs, their badges and the totals are computed from it |
| Russian stage names | `stageKey` is a `Skill.Stage` value and the batch DTOs carry no label | `GET /skills/stages`, with `getStageMeta` falling back to the raw key |

---

## Slice 7 — Свои версии контента и редактор урока (O14, O15, O19)

Screens: `/org/content/overrides` (O14), `/org/content/overrides/[kind]/[overrideId]` (O15),
`/org/content/lessons/[lessonId]` (O19). Design: `docs/TENANCY/ADMIN_UI_DESIGN.md` O14/O15/O19,
§6.3, §7. Semantics: `docs/TENANCY/CONTENT_MODEL.md` §1, §2.3–2.6.

The one thing this slice must never grow is a merge. The API returns no diff on purpose, the client
computes none, and there is no «слить автоматически» button anywhere — a three-way merge of prose
and grading criteria produces plausible nonsense that then scores a live salesperson.

### Endpoints each screen calls

| Screen | Endpoints |
|---|---|
| O14 `/org/content/overrides` | `GET /admin/content/overrides` (learning) **and** `GET /admin/dialog/overrides/modes` (ai) — both unfiltered; the «только устаревшие / все» chips partition rows the server already flagged, so both counters come from one read per service |
| O15, three learning kinds | `GET /admin/content/overrides/{kind}/{overrideId}`, `POST …/accept-base`, `POST …/keep-override`; for a lesson also `GET /admin/lessons/{baseId}/versions`, purely to turn the review's version **ids** into the «версия 3» / «версия 5» column headings |
| O15, `kind=modes` | `GET /admin/dialog/overrides/modes/{overrideId}`, `PUT /admin/dialog/overrides/modes/{overrideId}` (inline prompt editor), `POST …/accept-base`, `POST …/keep-override` |
| O19 `/org/content/lessons/[lessonId]` | `GET /admin/lessons` (title/topic — there is no by-id route), `GET /admin/content/overrides` (ownership), `GET /admin/lessons/{id}/versions`, `GET /admin/lessons/{id}/exercises`, `GET /admin/lessons/{id}/accuracy`, `PUT /admin/lessons/{id}`, `POST /admin/lessons/{id}/exercises`, `PUT /admin/exercises/{id}`, `DELETE /admin/exercises/{id}`, `POST /admin/lessons/{id}/versions/draft`, `POST /admin/lessons/{id}/versions/publish`, `POST /admin/content/overrides/lessons/{baseId}` (the «Сделать свою версию» button) |

### Automated (vitest)

| File | Covers |
|---|---|
| `__tests__/orgContentOverrides.test.ts` | the override state vocabulary, the two-service row merge, comparison blocks, the accuracy series, the lesson's version state, exercise summaries and reordering |
| `__tests__/OrgContentOverridesComponents.test.tsx` | `ThreeWayCompare`, `OverrideStateBadge`, `PublishDialog`, `UnpublishedDraftBanner`, `AccuracySeriesChart` |

The behaviours worth naming, because they are the ones that break quietly:

- **Four override states, not one boolean.** `isStale && forkedFrom !== null` → «оригинал
  обновился»; `isStale && forkedFrom === null` → «основа неизвестна» (40.15 left this expressible
  on purpose and it must not be rounded into the first); `!isStale && baseCurrent === null` → «у
  оригинала нет версий», which is *not* «совпадает с базой»; otherwise «совпадает с базой».
- **Nothing recomputes staleness on the client.** `resolveOverrideState` reads the server's three
  fields and compares no content.
- **One table, two services, unique row keys.** An override id is unique only inside its own
  service, so rows are keyed `${kind}:${overrideId}`. Stale rows sort first.
- **Highlighting is block-level and has no button.** `alignComparisonBlocks` answers only «этот
  блок отличается», by whole-string equality; a block missing from one column counts as differing
  rather than being dropped. The compare component renders zero buttons.
- **`schemaVersion` is never shown as content.**
- **Segments never join.** Each accuracy segment is its own polyline; a segment starting at a
  breaking publish draws a visible break.
- **`unversionedAttempts` is a footnote, never version 1**, with Russian plural agreement
  (1 попытка / 3 попытки / 11 попыток / 21 попытка).
- **A segment with `attemptCount: 0` draws a hollow point**, because «никто не отвечал» and «версии
  нет» are different answers; `accuracy` arrives as 0..1 and is shown as whole percents.
- **The publish dialog has no default.** Its confirm button stays disabled until one of the two
  scopes is chosen, and it passes `isBreaking` verbatim.
- **`createdNewVersion: false` shows «Изменений нет — публиковать нечего»** and the version number
  does not move.
- **No XP, streaks or leagues** anywhere in the slice's vocabulary — asserted directly.

### Manual — O14 `/org/content/overrides`

Preconditions: a `TenancyAdmin`, at least one lesson override and one dialog-mode override.

| Scenario | Expect |
|---|---|
| An organization that has overridden nothing | «Своих версий нет — вы читаете общую библиотеку целиком…», framed as the healthy state, no error styling, no «создать копию» button |
| Every copy up to date | the «Только устаревшие 0» chip is selected and the table says «Устаревших копий нет», not the empty-library text |
| A stale lesson and a fresh mode | one table, four kind labels («урок», «техника», «справка», «режим диалога»), stale rows on top |
| Stop ai-service, reload | the three learning kinds still render, above a «Режимы диалога сейчас недоступны» line; the page is not an error |
| Stop learning-service, reload | the whole screen is `ErrorState` with a retry — the learning list *is* the screen |
| Look for a «сделать копию» button | there is none, anywhere |
| Click a row | O15 for that kind |

### Manual — O15 `/org/content/overrides/[kind]/[overrideId]`

| Scenario | Expect |
|---|---|
| Open a stale **lesson** override | three columns: «База на момент копирования (версия N)», «Ваша версия», «База сейчас (версия M)» |
| Open a **technique** or **справка** override | two columns plus «Каким оригинал был в момент копирования, мы не знаем — у этого типа материалов нет истории версий.» |
| Look for a merge control | none: three actions only, and the «Мы не сливаем эти тексты автоматически…» block under the columns |
| «Оставить своё» | no confirmation — it is cheap and reversible; the state badge flips to «совпадает с базой» after the refetch |
| «Взять базу» | a confirmation saying the copy goes to the archive and is not deleted; on success, back to O14 |
| «Править» on a lesson | O19 for that override |
| «Править» on a technique/справка | the platform panel (`/admin/techniques`, `/admin/reference`) with the «редактирование техник и справок пока делается через платформенную панель» caption — §6.3, still open |
| Open a **dialog mode** override | two columns of prompts; «Править» expands the two monospace fields in place, with «Сохранить» |
| Save the prompts | one `PUT`; there is no publish step and the fork mark clears itself |
| A bad `kind` in the URL | «Неизвестный тип материала», not a crash and not a blank page |

### Manual — O19 `/org/content/lessons/[lessonId]`

| Scenario | Expect |
|---|---|
| Open a lesson override with a live draft | the sticky «Есть неопубликованные правки» bar naming the version the team is still answering; it stays visible while scrolling |
| Edit an exercise | the draft is opened first (`POST …/versions/draft`), so the bar appears immediately rather than after the next reload |
| Press «Опубликовать» | the modal with two radio options and no default; the button is dead until one is chosen |
| Publish with nothing changed | «Изменений нет — публиковать нечего»; the version number does not move and the modal stays open |
| Publish «по смыслу» | the accuracy chart gains a dashed break before the new segment |
| Try to close the tab with a live draft | the browser's own leave prompt |
| Press «← Контент» with a live draft | an in-app dialog: «Уйти без публикации» or «Опубликовать сейчас» |
| Reorder with ↑/↓ | positions renumber 1..n; each move is one `PUT` per exercise that actually moved |
| Open a **global** lesson | read-only, with «Сделать свою версию»; no title field, no per-row edit buttons |
| Press «Сделать свою версию» on a global lesson | one `POST`, then a redirect to the copy's own id |
| Open a lesson that is the organization's own but is **not** an override (generated by O11) | read-only at first; pressing «Сделать свою версию» answers 409 and the screen unlocks editing instead of showing an error |
| Look at the accuracy chart with no published versions | «У урока ещё нет опубликованных версий, поэтому точность не по чему считать» |
| Break `GET …/accuracy` alone | a compact error inside the chart card only — the exercises above stay editable |
| Look for XP, streaks or leagues | none |

### What the backend cannot serve, and what the screens do instead

| Design asks for | Reality | Degraded behaviour |
|---|---|---|
| «Урок с `organizationId == null` открывается только на чтение» | **no endpoint returns a lesson's owner.** `GET /admin/lessons` is `(id, topicId, topicIconicName, topicTitle, title, orderInTopic)` and there is no `GET /admin/lessons/{id}` at all | ownership is inferred from `GET /admin/content/overrides`: a lesson listed there as a `lessons` override is the organization's copy. A lesson the organization owns without being an override (an O11-generated one) reads as global until «Сделать свою версию» answers 409 `SourceNotGlobal`, which the screen treats as «already yours» and unlocks editing. Every write is additionally guarded server-side by `ContentAuthoringGuard`, so the worst case is a 403 with a plain sentence, never a wrong write |
| Column headings «(v3)» / «(v5)» on O15 | `ContentOverrideReviewDto` carries version **ids**, not numbers | one extra `GET /admin/lessons/{baseId}/versions` for lesson overrides, mapping id → number; the heading falls back to «точка форка» / «текущий оригинал» when the id is not in that list |
| «`is_breaking` derived from the edit» | impossible in principle — a fixed comma and a moved correct answer are the same diff | the publish modal asks, with no default |
| Per-version accuracy points | `LessonAccuracySegmentDto` aggregates a whole segment into one statistic | every version inside a segment is drawn at that segment's value, so the axis stays honest about how many versions the run covers without inventing per-version numbers |
| Russian field labels **inside** the twelve exercise editors | `features/admin/components/exercise-editors/*` is the platform panel's code and its field captions are English | the editors are reused whole (`{content, onChange}`); only the type names are Russian, in `features/org-content-overrides/utils/exercise-summary.ts`. Translating the captions means editing `features/admin/**`, which slice 7 does not own — still open |
| Editing techniques and reference materials in this panel | §6.3: no organization-panel screens exist for them | «Править» links out to `/admin/techniques` / `/admin/reference` with the caption saying so |
| Drag-and-drop reordering | there is no batch reorder route; `PUT /admin/exercises/{id}` moves one row | ↑/↓ buttons, one write per exercise that moved |

---

## Slice 8 — O16 «Люди» (`/org/people`)

Invites and the roster. Design:
[docs/TENANCY/ADMIN_UI_DESIGN.md → O16](../TENANCY/ADMIN_UI_DESIGN.md#o16--orgpeople--люди).
Semantics: [docs/TENANCY/TENANCY.md §4](../TENANCY/TENANCY.md) (closed access, invites, offboarding).
Contract: [docs/API_CONTRACTS.md → «Invites & memberships»](../API_CONTRACTS.md).

Covers `app/(org)/org/people/page.tsx` and `features/org-people/**`.

### Endpoints the screen calls

| Method | Path | Gate | Used for |
|---|---|---|---|
| GET | `/memberships?status=active\|all` | `RequireOrgAdmin` | «Состав команды», both filter chips |
| GET | `/invites?status=pending\|all` | `RequireOrgAdmin` | «Приглашения», both filter chips |
| POST | `/invites` `{emails[], role}` | `RequireOrgSuperAdmin` | bulk invite |
| DELETE | `/invites/{inviteId}` | `RequireOrgSuperAdmin` | «Отозвать» |
| DELETE | `/memberships/{userId}` | `RequireOrgSuperAdmin` | «Отключить» — deactivation, not deletion |

**The design's §6.1 palliative is not built.** `GET /memberships` and `GET /invites` were added on
2026-08-18; the screen reads both directly. There is no in-memory «отправлено только что» list that
disappears on reload, and the roster is not assembled out of «кто хоть что-то решал» — a manager
hired last week with no attempts is on it, and so is the person who left.

### Automated (vitest, from `src/frontend`)

```
npx tsc --noEmit
npx vitest run
npx eslint "app/(org)/org/people" features/org-people
```

| File | Covers |
|---|---|
| `__tests__/orgPeopleLogic.test.ts` | `parseInviteEmails`, `buildInviteOutcomeLines`, `summarizeInviteOutcome`/`describeInviteOutcome`, the four dictionaries (`describeInviteStatus`, `describeInviteRejection`, `describeOrganizationRole`, `describeMembershipStatus`), `formatShortRussianDate`/`formatLongRussianDate`, `describeMemberName`/`buildMemberInitials` |
| `__tests__/OrgPeopleScreen.test.tsx` | `InviteOutcomeList`, `PendingInvitesTable`, `RosterTable`, `ReadOnlyNotice` rendered with `@testing-library/react` |

The behaviours worth naming:

- **A partial answer is one list, in the order the addresses were pasted.** `created[]` and
  `rejected[]` are merged and re-sorted by submission order, matched on the trimmed lower-cased
  address, because the server normalizes what it accepts and echoes back verbatim what it could not
  parse. Two separate blocks would make «третья строка не прошла» unanswerable.
- **A bulk invite where three of forty failed reads as neither success nor failure**:
  «Отправлено приглашений: 37 · отклонено адресов: 3». Both numbers, always.
- **The client never de-duplicates and never lower-cases the pasted list.** That is what produces
  `duplicate-in-request` on the server, and swallowing it client-side would hide from the РОП that
  their spreadsheet column had the address twice.
- **All four invite states have distinct wording** — «Ждёт ответа» / «Принято» / «Отозвано» /
  «Истекло» — and the dictionary covers exactly those four codes. The browser never recomputes
  «истекло» against its own clock; the status is derived server-side, with recorded facts
  outranking it.
- **An unknown status, role or rejection reason renders verbatim**, never as «неизвестно» and never
  as a guess. A value the dictionary has not heard of is a contract change and has to be visible.
  `OrgAdmin`, retired on 2026-08-16, is deliberately absent and therefore shows as `OrgAdmin`.
- **The raw token is never rendered.** `CreatedInvite` in `types/organization-people.ts` has no
  `token` field at all, so nothing downstream can print it; the test feeds a response that *does*
  carry one and asserts it appears nowhere in the DOM, and that the outcome list contains no `<a>`.
  The screen says instead that the link went to the mailbox and why it is not shown.
- **«Отключить» is offboarding.** The confirmation says the person loses access and that their
  progress, conversations and assignment rows stay — «это история компании» — and the word «удалить»
  appears nowhere in `features/org-people/**`.
- **A deactivated person stays on the list** under «С отключёнными», dated, with no button left.
- **The superadmin is not offered a button that deactivates themselves.** Their own row carries
  «это вы» and no action. The backend would allow it; locking yourself out of your own organization
  is not a thing a screen should make easy.
- **There is no role control anywhere** — no select in a roster row, no «сменить роль». §6.2 is
  real: the route does not exist. The screen says so in one line under the table instead of leaving
  the reader hunting for it.
- **A `TenancyAdmin` sees the same two lists with no write controls at all** and a sentence saying
  who may invite and offboard — not a row of disabled buttons, which reads as breakage.
- **The two reads fail independently.** A dead invite read shows its own `ErrorState` above a roster
  that still renders, and says so.
- **Loading shows `DataTable`'s skeleton, never an empty state**; an empty pending queue explains
  what would appear there instead of reporting a zero.
- **No XP, no streaks, no leagues** — asserted over the rendered output.

### Manual — O16 `/org/people`

Preconditions: a `TenancySuperAdmin`, a plain `TenancyAdmin` in the same organization, and one
member who can be offboarded.

| Scenario | Expect |
|---|---|
| Open as `TenancySuperAdmin` | invite form, invite queue, roster; «Отправить» disabled until the field has an address |
| Paste four addresses, one per line | the button reads «Отправить 4» before you press it |
| Paste `a@x.ru, b@x.ru; c@x.ru` on one line | still three addresses — comma, semicolon and newline all split |
| Invite two good addresses and one that is already a member | one list of three: two ✓ with «действует до …», one ✗ «уже в компании»; the summary names both counts |
| Invite the same address twice in one paste | one ✓ and one ✗ «повторяется в списке» — the client does not silently collapse them |
| Invite an address with a pending invite | ✗ «приглашение уже отправлено» |
| Type `не-адрес` | ✗ «непохоже на адрес»; the other addresses in the same paste still go out |
| Look for the invite link anywhere on screen | there is none, and a line explains that it went to the mailbox |
| «Отозвать» on a pending invite | it disappears from «Ждут ответа»; under «Все» it reappears as «Отозвано» with no button |
| Switch the invite filter to «Все» | accepted, revoked and expired invites appear, each with its own word |
| «Отключить» a member | the dialog names them and says the history stays; after confirming they leave «Работают» |
| Switch the roster filter to «С отключёнными» | they are there, «Отключён», dated, with no button |
| Find your own row | «это вы», and no «Отключить» on it |
| Look for a way to change somebody's role | there is none; the line under the table says a new invite is the way |
| Open as a plain `TenancyAdmin` | both lists in full, no invite form, no «Отозвать», no «Отключить», and the sentence saying why |
| Stop identity-service | both sections show their own `ErrorState` with a working «Повторить» |
| A brand-new organization | the roster is exactly one person — you; the invite queue explains itself instead of showing zero |

### What the backend cannot serve, and what the screen does instead

| Design asks for | Reality | Behaviour |
|---|---|---|
| «Иванов А. · менеджер · попыток 214» in the roster | `MembershipDto` carries no attempt count, and the heat map that has one is O1's read in another service | the roster shows role, joining date and status; attempts stay on `/org` where they are measured |
| A control that changes a member's role | §6.2 — `PUT /memberships/{userId}/role` does not exist | no control; one line saying a new invite is the only way |
| «Отправлено только что» as an in-memory palliative that vanishes on reload | superseded — `GET /invites` exists | the section renders the real answer to the last request, and the invite queue below it is read from the server |
| The roster assembled from people with activity | superseded — `GET /memberships` exists | the real roster, including people who have practised nothing |
| The invitee's name on a pending invite | there is no user row until the invite is accepted | the address, which is all that exists |
| `invitedBy` shown as a name | `InviteSummaryDto` carries the inviter's id and no name, and no route resolves one | not rendered — an id in that column would say nothing |

---

## Slice 10 — O18 «Программа обучения» (`/org/program`)

What it covers: `app/(org)/org/program/page.tsx` and `features/org-program/**`. Design:
[docs/TENANCY/ADMIN_UI_DESIGN.md → O18](../TENANCY/ADMIN_UI_DESIGN.md#o18--orgprogram--программа-обучения).
Semantics: [docs/TENANCY/CONTENT_MODEL.md §2.5](../TENANCY/CONTENT_MODEL.md) and the 40.17 entries in
[docs/DONT_FORGET.md](../DONT_FORGET.md).

**Endpoints.** Seven, all `AdminProgramController` (`RequireOrgAdmin`), plus identity-service's
roster:

| Route | Used for |
|---|---|
| `GET /admin/program/versions` | the version list, the draft row, the counters «47 уроков · зачислено 9» |
| `GET /admin/program/versions/{id}` | «Посмотреть» — the ordered items of one version |
| `GET /admin/program/versions/{id}/diff/{baselineId}` | «Что изменилось» and «Что изменится у него» |
| `POST /admin/program/versions/draft` | «Пересобрать черновик из дерева» |
| `POST /admin/program/versions/publish` | «Опубликовать» |
| `GET /admin/program/enrollments` | the enrollment table and the spread summary |
| `POST /admin/program/enrollments {userId}` | «Зачислить ещё», one person per call |
| `GET /memberships?status=active` | names for the enrollment rows, and the list «Зачислить ещё» offers |

**The route this screen must never gain.** There is no control anywhere on O18 that moves another
person's pin, and no route that would let one exist. `POST /admin/program/enrollments` is idempotent
and returns an existing enrollment unchanged; the move is `POST /program/switch`, which only the
learner calls on themself. A «перевести всех на v4» button would convert the guarantee «программу
под учащимся никто не переставит» from a property of the code into a question of what the panel drew
(ADMIN_UI_DESIGN.md §7, DONT_FORGET.md → блок 40.17). The paragraph saying so is on the screen on
purpose and is part of the guarantee, not decoration.

### Automated (vitest, from `src/frontend`)

```
npx tsc --noEmit
npx vitest run
npx eslint "app/(org)/org/program" features/org-program
```

| File | Covers |
|---|---|
| `__tests__/orgProgramVersions.test.ts` | `selectCurrentPublishedVersion`, `selectDraftVersion`, `selectPreviousPublishedVersion`, `isEnrollmentBehind`, `summarizeEnrollmentSpread`, `selectEnrollableMembers`, `buildMemberNameLookup`, and the `format-program-text` helpers |
| `__tests__/orgProgramComponents.test.tsx` | `EnrollmentSpreadSummary`, `EnrollmentTable`, `ProgramDiffView` rendered with `@testing-library/react` |

The behaviours worth naming, because they are the ones that mislead quietly:

- **«Отстаёт» is decided by version id, not by the version number.** `isEnrollmentBehind` compares
  `enrollment.programVersionId` against the newest published version's id: the id is what the pin
  stores, the number is a label two lists agree on by convention. A pin carrying number 3 that points
  at `version-2` is behind, and the test says so.
- **Nobody is behind when nothing is published.** With no published version the whole team is on the
  live tree — a different statement from «all up to date», and the enrollment table shows neither
  «Отстаёт» nor «Последняя» in that state.
- **The draft is never «the current version».** `selectCurrentPublishedVersion` ignores it even
  though its number is the highest; nobody can be pinned to a draft.
- **People who hold no pin are counted and named.** `summarizeEnrollmentSpread` returns
  `notEnrolledCount` against `GET /memberships?status=active`, and the summary says those people
  learn off the live skill tree. A screen that reports «2 из 3 на последней версии» and stays silent
  about the other four people in the organization has told the reader the programme is in force when
  it is not — that sentence is the reason this slice exists.
- **A roster still loading is not «никого нет».** `rosterState` is `loading` / `ready` /
  `unavailable`, because both of the non-ready states leave the unenrolled count at zero and the
  cheerful sentence would then be false rather than absent.
- **The mixed state is worded as normal.** Two versions in use renders «Команда учится по разным
  версиям… Это нормальное состояние, а не рассинхронизация», not a warning to be resolved.
- **The enrollment table offers exactly one button per behind row** — «Что изменится у него», which
  reads the diff. The render test asserts the full list of buttons in the table and that the word
  «Перевести» appears nowhere in it.
- **The diff is four sections, never one list**, and an empty bucket prints no heading. A moved
  lesson carries the footnote that its content did not change, which is the whole point of the fourth
  bucket. Nothing is computed on the client (§7).
- **`hasBreakingChanges` renders the red line** «в некоторых уроках изменился правильный ответ или
  критерии оценки», independently of whether any individual `changedLesson.isBreaking` is set.
- **A `null` `lessonTitle` renders «Урок недоступен»**, never the live lesson's title — substituting
  the live title is exactly the failure programme pinning exists to prevent.
- **Russian pluralization** agrees with the design mock: «1 урок» / «2 урока» / «47 уроков», the
  11–14 exception, and «1 человек» / «2 человека» / «9 человек» where the one-form and many-form are
  spelled the same.

### Manual

Preconditions: a `TenancyAdmin` account in an organization with a published skill tree. Nothing is
pre-seeded — on a fresh install every organization has zero programme versions and zero enrollments
(DONT_FORGET.md → блок 40.17), so the empty state is the first thing you will see.

| Scenario | Expect |
|---|---|
| Open `/org/program` on a fresh organization | the «Программа ещё не опубликована» card explaining the live tree, one primary button «Пересобрать черновик из дерева» — no empty table |
| Press it | a draft row appears with its lesson count; «Посмотреть», «Пересобрать черновик из дерева», «Опубликовать» |
| «Посмотреть» on the draft | the ordered lessons, each with the snapshot it is pinned to; a lesson whose snapshot is invisible shows «Урок недоступен», not a live title |
| «Опубликовать» | a confirmation saying the version freezes forever and that nobody already learning is moved; on confirm, «Опубликована v1. Никто из тех, кто уже учится, не сдвинулся.» |
| «Опубликовать» twice with no edits in between | rebuild the draft, publish → «Изменений нет, новая версия не создана»; the version number does **not** advance |
| «Опубликовать» with no draft at all | «Черновика нет. Соберите его из дерева навыков и попробуйте снова.» (the 409) |
| «Зачислить ещё» | a dialog naming the version people will land on and the sentence «Зачислит новичков и не тронет тех, кто уже учится»; one button per person |
| A hire who has never opened a lesson | they are in the dialog: the list is `GET /memberships?status=active`, not «кто что-то решал» |
| Stop identity-service, reload | the version list and the enrollment table still render; names fall back to «Без имени · {8 символов id}» and the summary says the roster did not load instead of «без зачисления никого нет» |
| Enroll somebody, then edit the tree, rebuild the draft, publish v2 | that person stays on v1 and their row gains «Отстаёт»; the summary reports the split, and nothing on the screen offers to move them |
| «Что изменилось» on v2 | four sections; a pure reorder puts everything in «Переставлены» and leaves the other three empty |
| «Что изменилось» on the first published version | the button is absent — there is no baseline, and a disabled button explaining itself would be worse |
| «Что изменится у него» on a behind row | the same diff, from that person's pinned version to today's |
| Look for a control that moves somebody | there is none. The paragraph under the table says why, and that paragraph is a requirement, not copy |
| Stop learning-service | `ErrorState` with a working «Повторить»; the diff dialog has its own error branch |
| A learner switches themself (`POST /program/switch`) | their row reads «перешёл сам 14 авг» instead of «зачислен …» |

### What the backend cannot serve, and what the screen does instead

| Design asks for | Reality | Degraded behaviour |
|---|---|---|
| «Иванов А. v3 зачислен 12 авг» | `ProgramEnrollmentDto` carries `userId` only — learning-service holds no replica of a person's name | names come from identity-service's `GET /memberships?status=active`, which landed with slice 8; a pin whose person is not on the active roster (deactivated, or the request failed) renders «Без имени · {8 символов id}» |
| A total of «сколько человек в организации не зачислено» | answerable now, but only against a roster that loaded | `rosterState` is three-valued: «Без зачисления — N человек» when the roster is ready, «Без зачисления никого нет» when it is ready and empty, and a note saying the roster did not load otherwise — a request still in flight must not render as «никого нет» |
| Enrolling a group in one action | there is no bulk route, deliberately (DONT_FORGET.md → блок 40.17) | one `POST` per person, driven by a per-row button; no multi-select that could later be pointed at existing pins |
| The pinned programme actually driving what a learner sees | `/skill-tree`, `/lessons` and `/exercises/*` still read the live tree; only `GET /program` serves the pin, and no learner screen calls it yet | a footnote on O18 says exactly that, so that publishing is not read as «команда уже учится по этой версии» |
| Un-enrolling somebody | no route exists | none offered |

---

## Slice 11 — `/admin/organizations/[organizationId]/quota` (the platform-side addition)

Everything above this heading is the **organization** panel (`/org/*`, Russian). This one section is
the block's single addition to the **platform** panel (`/admin/*`, English,
[ADMIN_UI_DESIGN.md §3.2](../TENANCY/ADMIN_UI_DESIGN.md)) and lives here only because the platform
panel has no testing doc of its own.

### Automated (vitest)

| File | Covers |
|---|---|
| `__tests__/adminOrganizationQuota.test.tsx` | the three hooks, the formatting and validation rules in `features/admin/lib/organization-quota-format.ts`, and the screen's four states |

The behaviours worth naming:

- **An unpriced model is words, never a zero.** `estimatedCost: null` renders as `No price`, the
  total refuses to print while `hasUnpricedModels` is true *even when an amount is present*, and the
  rendered page contains no `0.00 ₽`. Pinned both at the formatter and in the DOM.
- **The write body never carries an organization id.** `PUT /admin/ai-quota` is tenant-scoped through
  `X-Organization-Id`; a body field would be a tenancy-boundary violation and would be ignored.
- **The two ceilings are two numbers.** `calculateBatchTokenCeiling` reproduces
  `ResolvedAiQuota.BatchTokenCeiling`, integer truncation and the 90% clamp included, so «remaining»
  is computed twice — once for background work, once for interactive.
- **An empty field is a reset, not a removal**, and an unset field renders empty rather than
  pre-filled with the default it would fall back to.
- **`0` is refused as a way of lowering a limit.** `AiSpendMeter` gates on `limit > 0`, so zero turns
  enforcement *off*; the field says so the moment it is typed.
- **A negative number and a reserve above 90 are refused client-side**, because ai-service accepts
  both and then silently reinterprets them (negative → `null`, i.e. a reset; >90 → clamped).
- **No gamification**: the rendered page is asserted to contain no XP, streak or league.

### Manual

Preconditions: the app running, and a platform `Admin` or `SuperAdmin`.

| Scenario | Expect |
|---|---|
| Open `/admin/organizations`, press «Quota» on a row | the screen, headed with that organization's name, slug and status from `GET /organizations/{id}` |
| Session scoped to that same organization | the form is editable; saving reports «Quota saved» and the effective captions move |
| Session with no organization (the ordinary platform staff case) | a warn banner, every field disabled, and the spend panel labelled as the installation-wide total |
| Organization with no `OrganizationQuotas` row | «not unmetered — every number below is the platform default and is already being enforced» |
| Type `0` into a limit | the warn line saying zero removes the ceiling instead of closing it |
| Type `95` into the reserve | refused before the request, naming the 90% cap |
| Break `GET /admin/ai-usage` alone | the spend panel shows its own error; the form above stays editable |
| Look for XP, streaks or leagues | none |

### What the backend cannot serve, and what the screen does instead

| Design asks for | Reality | Degraded behaviour |
|---|---|---|
| A screen at `/admin/organizations/[organizationId]/quota` that edits *that* organization | `GET`/`PUT /admin/ai-quota` take no organization anywhere. The tenant is `X-Organization-Id`, which `Gateway/IdentityForwarding.cs` strips from the request and re-adds **only** from the token's `org_id` claim | the route parameter is treated as a claim to check, not a parameter to send: the screen compares it with the session's own `orgId` and disables saving unless they match |
| «доступен только внутри impersonation» (§3.2) | impersonation mints `role: User` (`PlatformAdminService.BuildImpersonationAccessToken`), which fails `RequirePlatformAdmin` on both quota routes — and `app/(admin)/layout.tsx` would bounce the session out of the panel first | the «Quota» link goes straight to the screen without impersonating. Editing therefore works only for platform staff who also hold a membership in that organization; the banner says exactly that |
| `PUT` for an organization the operator is not scoped to | ai-service throws `"Organization context is not set."` → 500 | saving is disabled rather than attempted |
| Per-model money on the report | the price table is `AiQuotas:PricePerMillionTokens` in ai-service configuration and no endpoint reads or writes it | shown read-only, with the copy separating «the token limit gates calls» from «the price table only changes reports» |

**One documentation discrepancy found and not fixed here:** `docs/AI_QUOTAS.md` §2 says «`0` in any
limit disables that window explicitly. Null and zero mean different things on purpose.» The code
disagrees — `AiSpendMeter`'s LLM gate, its Lua reserve script and `DescribeState` all short-circuit on
`limit > 0`, so zero means *no ceiling*, the same as an unbounded window. The screen warns about it in
place; the doc still needs correcting by whoever owns `AI_QUOTAS.md`.
