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
