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
