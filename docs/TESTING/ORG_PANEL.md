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
