# Admin Panel — Design & Decisions

## Roles

> **2026-08-16 update (owner's request):** roles live on two independent axes. `Admin` and
> `SuperAdmin` are **Sellevate's own platform roles** and are deliberately not bounded by tenancy.
> Every organization additionally has its own `TenancyAdmin` and `TenancySuperAdmin`. At either
> level the **only** difference between the admin and the superadmin is that only the superadmin may
> **add or remove users**. See `docs/DECISIONS.md` (2026-08-16) for the rationale and the full
> route audit.

### Platform role (`User.Role`)

| Role | Value | Capabilities |
|---|---|---|
| `User` | 0 | Regular learner — existing experience unchanged |
| `Admin` | 1 | Sellevate staff. Every `/admin/*` content endpoint below and the `/organizations` tenant registry. **May not** add, invite, deactivate or re-role a user |
| `SuperAdmin` | 2 | Everything `Admin` can do, **plus** adding/removing users and impersonation |

Value 1 was `Admin` before Phase 40.6 removed it and is `Admin` again — the same meaning, so no
stored row changes interpretation.

Role is stored as an integer column on the `User` table and emitted as a `role` claim in the JWT access token.

### Organization role (`membership.role`)

| Role | Value | Meaning |
|---|---|---|
| `Manager` | 0 | The salesperson practicing in one organization |
| `TenancyAdmin` | 1 | The РОП — admin of that one organization (formerly `OrgAdmin`, same value) |
| `TenancySuperAdmin` | 2 | Everything `TenancyAdmin` can do, **plus** inviting and offboarding that organization's users |

Emitted as the `org_role` JWT claim (alongside `org_id`) when the user has an active
`membership` row; **absent** for a user with none — which is the normal state for Sellevate staff,
and why the platform role satisfies the org-scoped policies on its own. The organization-scoped
panel these two roles open is `/org/*` — see [Two panels](#two-panels-4020) below.

The frontend mirrors the pair in `shared/stores/auth-store.ts` as `isOrganizationStaff(orgRole)`
and `canManageOrganizationPeople(orgRole)`, alongside the platform pair. They are display gates
only; the backend policies are what enforce the rule.

---

## Authorization policies (backend)

| Policy | Satisfied by | Applied to |
|---|---|---|
| `RequirePlatformAdmin` | `role` ∈ {`Admin`, `SuperAdmin`} | All `/admin/*` **content** endpoints (learning, ai, gamification, social), the `/organizations` tenant registry, and the read side of `/admin/users` |
| `RequireSuperAdmin` | `role` = `SuperAdmin` | Everything that adds or removes a user platform-wide: `PUT/DELETE /admin/users/*`, plus all of `/admin/platform/*` (impersonation, bootstrap-admin) |
| `RequireOrgAdmin` | `org_role` ∈ {`TenancyAdmin`, `TenancySuperAdmin`} **or** `role` ∈ {`Admin`, `SuperAdmin`} | Everything the РОП does. Twenty controllers carry it as of 40.33 — see the list below the table |
| `RequireOrgSuperAdmin` | `org_role` = `TenancySuperAdmin` **or** `role` = `SuperAdmin` | `/invites`, `/memberships` — adding and removing an organization's users |

**`RequireOrgAdmin` was written in 40.6 with no call site and stayed empty until 40.20's design
existed.** Blocks 40.21–40.33 then hung the whole РОП surface on it, so "reserved for a future
screen" is no longer true — this is the busiest of the four policies. The controllers that declare
it, verified by `grep RequireOrganizationAdministrator src/backend/**/Features`:

| Service | Controllers |
|---|---|
| learning | `AdminAssignmentsController`, `AdminProgramController`, `AdminTeamInsightsController`, `AdminTeamSkillGapsController`, `AdminDialogReviewsController`, `AdminContentGenerationController`, `AdminContentAdaptationController`, `AdminContentOverridesController`, `AdminLessonsController`, `AdminLessonVersionsController`, `AdminLessonMetricsController`, `AdminExercisesController`, `AdminReferenceController`, `AdminTechniquesController` |
| ai | `AdminDialogSessionsController`, `AdminDialogOverridesController`, `AdminAiQuotaController` |
| organization | `OrganizationProfileController` — the three writing routes only (`PUT`, `PATCH`, `POST …/draft/apply`); the reads stay open to any member |
| identity | `InvitesController`, `MembershipsController` — reads only; every mutation on them is `RequireOrgSuperAdmin` |

Platform staff satisfy the two org-scoped policies **without holding any `org_role` claim**: they
normally have no membership anywhere, and the whole point of the platform roles is that they are not
bounded by tenancy. (Making the *data* they then see span every organization is a separate concern
in the tenancy layer, not in these policies.)

All four are declared once per service in `Common/Constants/AuthorizationPolicies.cs` and registered
with `builder.Services.AddAuthorization(AuthorizationPolicies.Register)`. Controllers use
`[Authorize(Policy = AuthorizationPolicies.RequirePlatformAdministrator)]` and friends — never a
string literal. Each service's test project carries an `AuthorizationPolicyContractTests` that pins
the wire-level names and the two asymmetries.

---

## Two panels (40.20)

Since block 40.20 there are **two** admin surfaces, at two addresses, and this document describes
both. The full screen-by-screen design is
[TENANCY/ADMIN_UI_DESIGN.md](TENANCY/ADMIN_UI_DESIGN.md); what follows is the part a person needs
before touching either tree.

| | Platform panel | Organization panel |
|---|---|---|
| Address | `/admin/*` | `/org/*` |
| Route group | `app/(admin)/` | `app/(org)/` |
| Audience | Sellevate staff | the customer's РОП |
| Gate | `isPlatformStaff(role)` | `isOrganizationStaff(orgRole) \|\| isPlatformStaff(role)` |
| Language | English (internal tool) | Russian, «вы»-form (a product surface the customer pays for) |
| Styling | raw Tailwind utilities | the `shared/components` library |
| Screens | the sixteen that already existed, unchanged | nineteen new ones (O1–O19) |

**Nothing moved.** The split is a second tree, not a migration of the first: no `/admin/*` screen
was renamed, rewritten or deleted, which is also what let the eleven screen slices be built in
parallel without colliding.

**Why `/org` and not `/admin/org`.** Nesting would have given the two panels one layout, one
language and one gate — exactly what the block separates.

**Platform staff with no membership** land in state O0 (`features/org-shell/components/no-organization-state.tsx`),
which explains that the panel shows one company's data and points at `/admin/organizations`, where
impersonation (40.9) is the logged way in. The state is decided by `authenticatedUser.orgId == null`,
not by a 403 — the 403 arrives per request and far too late to explain anything.
`ImpersonationBanner` is mounted in `app/(org)/layout.tsx` as well as in `app/(main)/layout.tsx`, so
somebody inside a customer's panel can always see whose it is and get back out.

**No gamification anywhere in `/org/*`** — no XP, no streaks, no leagues, even where an endpoint
returns those fields (`ActiveAssignmentDto.xpEarned`, `DialogSessionDto.xpEarned`). The РОП's only
numeric currencies are accuracy in percent and a dialog score out of 100.

### Legacy `/admin/*` links from notifications

The Phase 40.26 notification jobs mint two `actionUrl`s that point at organization-panel screens
under their old `/admin/*` addresses, and those rows are already in the notification store:

- `AssignmentDeadlineDigest` → `/admin/assignments/{id}?action=remind&scope=not_started`
- `DialogReviewDisputed` → `/admin/dialog-reviews?note={noteId}`

They keep working through a redirect table — `features/org-shell/lib/legacy-admin-redirects.ts`,
called from `app/(admin)/layout.tsx` **before** the role gate, with `router.replace` so Back leaves
the panel instead of bouncing off the redirect. Ten prefixes are mapped; the longest matching one
wins, so `/admin/dialog/overrides` beats `/admin/dialog`, which is a platform screen and redirects
nowhere. The query string is carried over whole.

| From | To |
|---|---|
| `/admin/assignments`, `/admin/assignments/{id}` | `/org/assignments`, `/org/assignments/{id}` |
| `/admin/dialog-reviews` | `/org/reviews` |
| `/admin/dialog-sessions` | `/org/dialogs` |
| `/admin/team` | `/org` |
| `/admin/content/overrides`, `/admin/dialog/overrides` | `/org/content/overrides` |
| `/admin/content-generation` | `/org/content/generation` |
| `/admin/content/adaptations` | `/org/content/adaptations` |
| `/admin/ai-usage` | `/org/usage` |

`app/(admin)/admin/[...legacyAdminPath]/page.tsx` exists only so the table can run at all: none of
those paths has a page of its own, and Next.js answers an unmatched URL with the global not-found
without rendering any route-group layout. The catch-all matches them so the layout above redirects;
anything the table does not recognise still 404s.

**The parameters are read, not obeyed.** `action=remind` opens the reminder confirmation and
`scope=not_started` preselects its recipients. The link never sends a reminder on load: a URL that
messages the team as it opens is a URL that fires the first time a mail scanner follows it.

---

## Admin distribution across microservices (Phase 9)

Every `/admin/*` endpoint now lives in the **service that owns the data**, not in a
central admin app. The frontend is unaffected: it calls the same paths through the
API gateway, which routes each `/admin/*` prefix to its owning service. `org_role`/`role`
are read straight off the JWT the service already validates (no header round-trip); the
gateway still injects `X-User-Id`/`X-User-Role`/`X-Organization-Id` for the cases that do
use headers. Each service registers the same four policies from its own
`AuthorizationPolicies.Register` and enforces them locally.

| Admin prefix | Owning service |
|---|---|
| `/admin/users/*` | identity-service |
| `/admin/skills`, `/admin/skill-stages`, `/admin/topics`, `/admin/lessons`, `/admin/exercises`, `/admin/exercise-type-prompts`, `/admin/reference`, `/admin/techniques`, `/admin/daily-quotes`, `/admin/seeder` | learning-service |
| `/admin/gamification/*`, `/admin/leagues/*` | gamification-service |
| `/admin/dialog/*`, `/admin/voice/*` | ai-service |
| `/admin/discuss/*` | social-service |

The monolith (`src/backend/api`) is retired — it no longer serves any `/admin/*`
traffic and its controllers remain only as reference. The gateway has no
`{**catch-all}` route, so an unknown route returns 404.

---

## Seeding the first SuperAdmin

On application startup (`Program.cs`), a default superadmin is upserted if no superadmin exists:
- Email: from env var `SUPERADMIN_EMAIL` (default `admin@sallevate.local`)
- Password: from env var `SUPERADMIN_PASSWORD` (default `Admin123!`)

Change these in production via environment variables.

---

## API endpoints (admin namespace)

All routes prefixed `/admin`. Require `RequirePlatformAdmin` unless noted.

### Skills
| Method | Path | Body | Response |
|---|---|---|---|
| GET | /admin/skills | — | `AdminSkillDto[]` |
| POST | /admin/skills | `{iconicName, title, description?, orderInTree, stage?}` | `AdminSkillDto` |
| PUT | /admin/skills/:id | `{iconicName?, title?, description?, orderInTree?, stage?}` | `AdminSkillDto` |
| DELETE | /admin/skills/:id | — | 204 |

### Skill Stages
| Method | Path | Body | Response |
|---|---|---|---|
| GET | /admin/skill-stages | — | `AdminSkillStageDto[]` |
| POST | /admin/skill-stages | `{key, label, accent, order}` | `AdminSkillStageDto` |
| PUT | /admin/skill-stages/:id | `{label, accent, order}` | `AdminSkillStageDto` |
| DELETE | /admin/skill-stages/:id | — | 204 |

The funnel stages used to group skills on `/tree` (`SkillStages` table). The `key` is immutable once created (it is stored on `Skills.Stage`); only `label`, `accent` color, and `order` are editable. A stage with skills still assigned to it cannot be deleted — reassign those skills first. Managed at `/admin/skill-stages`; read publicly at `GET /skills/stages`.

### Topics
| Method | Path | Body | Response |
|---|---|---|---|
| GET | /admin/topics | — | `AdminTopicWithSkillDto[]` (all topics) |
| GET | /admin/skills/:skillIconicName/topics | — | `AdminTopicDto[]` |
| POST | /admin/skills/:skillIconicName/topics | `{iconicName, title, orderInSkill}` | `AdminTopicDto` |
| PUT | /admin/topics/:id | `{iconicName?, title?, orderInSkill?}` | `AdminTopicDto` |
| DELETE | /admin/topics/:id | — | 204 |

### Lessons
| Method | Path | Body | Response |
|---|---|---|---|
| GET | /admin/lessons | — | `AdminLessonWithTopicDto[]` (all lessons) |
| GET | /admin/topics/:topicIconicName/lessons | — | `AdminLessonDto[]` |
| POST | /admin/topics/:topicIconicName/lessons | `{title, orderInTopic}` | `AdminLessonDto` |
| PUT | /admin/lessons/:id | `{title, orderInTopic}` | `AdminLessonDto` |
| DELETE | /admin/lessons/:id | — | 204 |

### Exercises
| Method | Path | Body | Response |
|---|---|---|---|
| GET | /admin/lessons/:lessonId/exercises | — | `AdminExerciseDto[]` |
| POST | /admin/lessons/:lessonId/exercises | `{type, orderInLesson, content: <jsonb>, customAiPrompt?}` | `AdminExerciseDto` (400 if content invalid for type) |
| POST | /admin/lessons/:lessonId/exercises/import | array `[{type, orderInLesson, content, customAiPrompt?}, …]` | `ExercisesImportResultDto` (per-item validation: bad items skipped, reported in errors) |
| PUT | /admin/exercises/:id | same | `AdminExerciseDto` (400 if content invalid for type) |
| DELETE | /admin/exercises/:id | — | 204 |

**Content validation:** The `content` field is validated server-side per exercise type. Single create/update return 400 with joined error messages on invalid content. Import validates each exercise and skips bad ones, reporting errors per item.

The exercises editor page has **Export JSON** (downloads the lesson's exercises as a re-importable array) and **Import JSON** (uploads such an array; upsert by `orderInLesson`). Business data such as users is intentionally not exportable.

### Exercise Type Prompts
| Method | Path | Body | Response |
|---|---|---|---|
| GET | /admin/exercise-type-prompts | — | `ExerciseTypePromptDto[]` |
| GET | /admin/exercise-type-prompts/:exerciseType | — | `ExerciseTypePromptDto` |
| PUT | /admin/exercise-type-prompts/:exerciseType | `{systemPrompt}` | `ExerciseTypePromptDto` |

**Two-level AI prompt model:** For AI-evaluated exercise types (6-10), prompts combine:
1. **Global type prompt** (stored in `ExerciseTypePrompts` table, edited at `/admin/exercise-type-prompts/:type`)
2. **Per-exercise prompt** (field `ai_prompt` inside `content` JSON)

The final prompt sent to the model is: `[global] + "Additional criteria:" + [per-exercise] + format instruction`.

### Reference Materials
| Method | Path | Body | Response |
|---|---|---|---|
| GET | /admin/reference | query: `?skillId=&category=&search=` | `AdminReferenceMaterialDto[]` |
| GET | /admin/reference/categories | — | `string[]` |
| GET | /admin/skills/:skillId/reference | — | `AdminReferenceMaterialDto[]` |
| POST | /admin/skills/:skillId/reference | `{title, markdownContent, sortOrder, category?, tags?}` | `AdminReferenceMaterialDto` |
| PUT | /admin/reference/:id | same | `AdminReferenceMaterialDto` |
| DELETE | /admin/reference/:id | — | 204 |

### Leagues
| Method | Path | Body | Response |
|---|---|---|---|
| GET | /admin/leagues | query: `?weekStart=&tier=` | `AdminLeagueListItemDto[]` |
| GET | /admin/leagues/weeks | — | `string[]` |
| GET | /admin/leagues/:id | — | `AdminLeagueDetailDto` |
| POST | /admin/leagues/close-current | — | 204 |
| POST | /admin/leagues/:id/resync | — | `AdminLeagueDetailDto` |
| PUT | /admin/leagues/memberships/:membershipId/tier | `{tier}` | `AdminLeagueDetailDto` |
| PUT | /admin/leagues/memberships/:membershipId/xp | `{delta}` | `AdminLeagueDetailDto` |
| DELETE | /admin/leagues/memberships/:membershipId | — | 204 |
| GET | /admin/leagues/settings | — | `LeagueSettingsDto` |
| PUT | /admin/leagues/settings | `UpdateLeagueSettingsRequestDto` | `LeagueSettingsDto` |
| GET | /admin/leagues/tiers | — | `AdminLeagueTierDto[]` |
| POST | /admin/leagues/tiers | `{key, name, color, order}` | `AdminLeagueTierDto` |
| PUT | /admin/leagues/tiers/:id | `{name, color, order}` | `AdminLeagueTierDto` |
| DELETE | /admin/leagues/tiers/:id | — | 204 |

Progress-point adjustments are NOT direct writes to `LeagueMemberships.WeeklyXpAmount` — that value is recomputed from `UserXpRecords` on every team-progress fetch and a direct write would be silently erased. Instead the adjustment is saved as a `UserXpRecords` row with `Source = "admin_correction"` (negative `Amount` allowed) stamped at the period's week start, then the group is re-synced. Group size / max participants, and the period schedule (`CurrentPeriodEndsAt`, `PeriodLengthDays`) live in the single-row `LeagueSettings` table. The tier ladder (key/name/color/order) lives in `LeagueTiers` and is managed at `/admin/leagues/tiers`; the key is immutable once created and a tier with existing groups cannot be deleted.

### Progress & Recognition (progress points economy)
The progress points economy is fully DB-driven; the controls are **distributed across the relevant admin sections**, not a single hub:
- **Per-exercise-type base points** → on the Exercise Type Prompts page (`/admin/prompts`).
- **Dialog points multiplier + criterion weights** → on the Dialog page (`/admin/dialog`).
- **Daily/weekly points goals + activity-consistency milestones** → on the Gamification page (`/admin/gamification`).

| Method | Path | Body | Response |
|---|---|---|---|
| GET | /admin/gamification/settings | — | `GamificationSettingsDto` |
| PUT | /admin/gamification/settings | `UpdateGamificationSettingsRequestDto` | `GamificationSettingsDto` |
| GET | /admin/gamification/exercise-rewards | — | `ExerciseTypeRewardDto[]` |
| PUT | /admin/gamification/exercise-rewards/:exerciseType | `{baseXpReward}` | `ExerciseTypeRewardDto` (upsert) |
| GET | /admin/gamification/streak-milestones | — | `StreakMilestoneDto[]` |
| POST | /admin/gamification/streak-milestones | `{dayCount, xpReward}` | `StreakMilestoneDto` (400 on duplicate day) |
| PUT | /admin/gamification/streak-milestones/:id | `{dayCount, xpReward}` | `StreakMilestoneDto` |
| DELETE | /admin/gamification/streak-milestones/:id | — | 204 |

See [API_CONTRACTS](API_CONTRACTS.md#gamification-xp) for DTO shapes and the points formulas. Validation: goals & multiplier positive, weights non-negative summing to > 0, `baseXpReward` non-negative, `dayCount` positive & unique.

### Daily Quotes
| Method | Path | Body | Response |
|---|---|---|---|
| GET | /admin/daily-quotes | query: `?from=&to=` (ISO dates) | `AdminDailyQuoteDto[]` ordered by date |
| POST | /admin/daily-quotes | `{date, text, author?}` | `AdminDailyQuoteDto` (409 if the date already has a quote, 400 on empty text) |
| PUT | /admin/daily-quotes/:id | same | `AdminDailyQuoteDto` |
| DELETE | /admin/daily-quotes/:id | — | 204 |

`AdminDailyQuoteDto`: `{id, date, text, author, createdAt, updatedAt}`. The admin UI is a month calendar (`/admin/quotes`) — click a day to create/edit/delete its quote.

### Users (read: `RequirePlatformAdmin`; every mutation: `RequireSuperAdmin`)
| Method | Path | Body | Response |
|---|---|---|---|
| GET | /admin/users | — | `AdminUserDto[]` |
| GET | /admin/users/:id | — | `AdminUserDetailDto` |
| PUT | /admin/users/:id | `{displayName}` | `AdminUserDto` — moderation rename (2–50 chars) |
| DELETE | /admin/users/:id/avatar | — | 204 — moderation: reset uploaded photo to default |
| PUT | /admin/users/:id/role | `{role: "User"\|"Admin"\|"SuperAdmin"}` | `AdminUserDto` (SuperAdmin only) |

`AdminUserDto`: `{id, email, displayName, role, createdAt, isEmailVerified, authProvider, hasCustomAvatar, avatarUrl}`.
`AdminUserDetailDto` adds activity stats: `{currentStreakDayCount, longestStreakDayCount, totalXpAmount, completedSkillCount, totalSkillCount, averageExerciseScore, persona}`.
UI: `/admin/users` lists all users (avatar, email + verification, provider, role); clicking a row
opens a detail modal. A platform `Admin` sees the roster and the modal **read-only** — renaming,
removing a photo and changing a role are all add/remove/re-role-a-user operations and are shown only
to a `SuperAdmin`, so an `Admin` is never offered a button that would answer 403.

**Owned by identity-service** (`AdminUsersController` in `identity-service/Identity/Features/Admin`). The activity stats (streak/XP/skills/score) are owned by gamification/learning, so identity returns them as `0` until cross-service composition lands — the same caveat as `GET /profile`. The monolith's copy stays as reference only.

### Organizations & impersonation (registry: `RequirePlatformAdmin`; `/admin/platform/*`: `RequireSuperAdmin`)

The `/admin/organizations` screen talks to two services, and the split follows which database the
operation needs. Full contracts in [API_CONTRACTS.md](API_CONTRACTS.md).

| Method | Path | Owning service | Purpose |
|---|---|---|---|
| GET / POST | /organizations | organization-service | list / create a tenant |
| POST | /organizations/:id/suspend, /organizations/:id/reactivate | organization-service | suspend / resume |
| POST | /admin/platform/organizations/bootstrap-admin | identity-service | invite the organization's first administrator, `TenancyAdmin` or `TenancySuperAdmin`, defaulting to `TenancySuperAdmin` |
| POST | /admin/platform/impersonation | identity-service | mint a short-lived token for another organization |
| GET | /admin/platform/impersonation | identity-service | the impersonation audit trail |

UI notes:

- **Impersonation always asks for a reason.** It is written into the audit record, and a crossing
  nobody can justify afterwards is the one nobody can review.
- Starting an impersonation swaps the active access token for the short-lived one and parks the
  platform token in `sessionStorage`. `ImpersonationBanner`, rendered in the main app shell on
  every screen, is the way back — without it, entering a customer organization would be a one-way
  door until the token expired.
- The "Impersonate" action is disabled for a suspended organization; the backend refuses it too.
- Suspending an organization stops its users signing in and stops their refresh tokens working.
  Already-issued access tokens keep working for up to 15 minutes.

### The РОП's dashboard (Phase 40.25 — API only, no screen yet)

`RequireOrgAdmin` throughout, so these are the first admin routes an *organization* administrator can
reach that are not part of the platform library. Full contracts in
[API_CONTRACTS.md](API_CONTRACTS.md); the design is
[TENANCY/ASSIGNMENTS.md](TENANCY/ASSIGNMENTS.md) §4.

| Method | Path | Owning service | Purpose |
|---|---|---|---|
| GET | /admin/assignments/:id/dashboard | learning-service | funnel, named rows, and every wave of the repeat series |
| GET | /admin/team/skill-map?days= | learning-service | skill heat map + each manager's weakest sales-funnel stage |
| GET | /admin/dialog-reviews?kind=&status=&sessionId= | learning-service | the review queue (open disputes, notes sent) |
| POST | /admin/dialog-reviews | learning-service | send a quoted fragment with a comment to the manager |
| POST | /admin/dialog-reviews/:noteId/resolve | learning-service | rule on a disputed AI score |
| POST | /admin/assignments/:id/remind?scope=unfinished\|not_started | learning-service | **(40.26)** the one-click nudge the deadline digest links to |
| GET | /admin/dialog-sessions?userId=&modeId=&maxScore=&limit= | ai-service | the team's graded conversations |
| GET | /admin/dialog-sessions/:sessionId | ai-service | one transcript, with per-message indexes |
| GET | /admin/team/skill-gaps?days= | learning-service | **(40.31)** what to do next: the failing funnel stages, and the ones deliberately not offered |
| POST | /admin/team/skill-gaps/:stageKey/content | learning-service | **(40.31)** the button — start a content run aimed at that stage |
| POST | /admin/team/skill-gaps/:stageKey/dismiss | learning-service | **(40.31)** «не сейчас» |
| DELETE | /admin/team/skill-gaps/:stageKey/dismiss | learning-service | **(40.31)** take the refusal back |

**The screens are 40.20's O1–O7, and they are designed.**
[TENANCY/ADMIN_UI_DESIGN.md](TENANCY/ADMIN_UI_DESIGN.md) draws every one of them against these
exact routes; slice 0 shipped the `/org/*` shell they hang off, and the screens themselves land in
slices 1–3. Until then these routes are reachable only by API. What did ship on the manager's side
of 40.25 is `/dialog-reviews` (their inbox) and the dispute link in the dialog feedback modal;
those are ordinary app screens, not admin ones.

Three notes for whoever builds the screen:

- **The funnel has five stages, not four.** `failedThresholdCount` is not a subset of
  `completedCount` — it is the people who finished the work and stayed under the bar, and the roadmap
  calls that the most valuable row on the screen. Drawing a four-stage funnel puts them back among
  the people who never started.
- **`isActiveMember` and the two roster counts can be `null`**, which means identity-service could not
  be asked. Say "could not check"; do not draw a zero. `rosterKnown` on the response is the flag.
- **`accuracyPercent` is `null` below `minimumAttemptsForAccuracy` attempts** and must render as a
  blank cell with an explanation, never as 0%. Two right answers out of two is 100% and is a fact
  about nobody.

**Phase 40.26 added a fourth note, and it is a hard requirement rather than advice.** The РОП now
receives two notifications, and both link into this unbuilt panel:

- `AssignmentDeadlineDigest` → `/admin/assignments/{id}?action=remind&scope=not_started`
- `DialogReviewDisputed` → `/admin/dialog-reviews?note={noteId}`

**Read the action out of the link; do not invent one.** `action=remind` means "open this assignment
with the reminder confirmation already up", and `scope=not_started` means "the recipients are exactly
the people the notification just listed by name" — the notice says «ещё не начали: Иванов, Петров»,
and a button that then messages everybody who has not *finished* is the screen contradicting the
notice that opened it. The endpoint in the table above already answers with that scope, so the screen
is the only missing piece. The link deliberately does **not** perform the reminder on load: a URL that
messages a team the moment it is fetched is a URL a mail scanner fires. Until the screen exists both
links 404 — recorded in [DONT_FORGET.md](DONT_FORGET.md).

**Phase 40.31 turned the dashboard into a tool, and left the whole thing invisible for the same
reason.** Four more routes, no screen. Four notes for whoever builds it, and the first is the block's
entire product claim:

- **The suggestion panel belongs beside the heat map, not on a page of its own.** «Отчёт открывают
  раз в квартал, инструмент — раз в неделю» is a claim about one screen: the red cell and the button
  that does something about it have to be in the same field of view. A separate «предложения» tab is
  a report about a report.
- **Render `suppressed`, do not drop it.** Every entry says why a real failure is not being offered
  (`dismissed` / `run_in_progress` / `recently_addressed`), until when, and — for the run cases — the
  run's id. A panel that shows nothing is indistinguishable from a broken one, and «почему мне ничего
  не предлагают» is the question that gets a feature switched off. `run_in_progress` should render as
  a link into the checkpoint screen below, not as a greyed-out button.
- **The button is one press and it is not instant.** `POST …/content` returns a
  `ContentGenerationJobDto` in `structuring`, `insufficient` or — if a run for that stage is already
  alive — the existing run, unchanged. All three are the same screen: the checkpoint screen of the
  section below. Do not build a second progress UI for this path.
- **Do not let the client assemble `sourceRef` or `sourceType`.** Both are returned by the server and
  both are derived again server-side when the assignment is created (`POST /admin/assignments` with
  `contentGenerationJobId`). A screen that posts `sourceType: "gap_detected"` by hand is a screen that
  can label anything as measured.

### JSON Import (Seeder)
| Method | Path | Body | Response |
|---|---|---|---|
| POST | /admin/seeder/skills | `multipart/form-data` with JSON file | `SkillsImportResultDto` |
| POST | /admin/seeder/topics | `multipart/form-data` with JSON file | `TopicsImportResultDto` |
| POST | /admin/seeder/lessons | `multipart/form-data` with JSON file | `LessonsImportResultDto` |

See [SEEDER.md](SEEDER.md) for JSON format and field details.

---

## Frontend routes

All routes under `/admin` are protected — non-admins are redirected to `/tree`.

```
app/(admin)/
  layout.tsx           ← sidebar nav + auth guard + the §1.5 legacy redirect table
  admin/
    [...legacyAdminPath]/
      page.tsx         ← catch-all so the redirect table sees the notification links; else 404
    page.tsx           ← redirect to /admin/skills
    skill-stages/
      page.tsx         ← funnel-stage CRUD (label/accent/order)
    skills/
      page.tsx         ← skill list + inline JSON import
      [id]/
        page.tsx       ← edit skill + topics list
        topics/
          [topicId]/
            page.tsx       ← edit topic + lessons list
            lessons/
              [lessonId]/
                page.tsx       ← lesson edit + exercises list
                exercises/
                  page.tsx     ← visual exercise editor (all 10 types)
        reference/
          page.tsx     ← reference materials list + editor
    topics/
      page.tsx         ← all topics view + inline JSON import
    lessons/
      page.tsx         ← all lessons view
    reference/
      page.tsx         ← global reference materials view
    techniques/
      page.tsx         ← technique CRUD + JSON import/export + skill linking (quick per-row
                          editor and select-many bulk toolbar, AD-3)
    prompts/
      page.tsx         ← exercise type AI prompts management
    quotes/
      page.tsx         ← daily quotes month calendar (click a day → edit quote)
    dialog/
      page.tsx         ← dialog bundles management
    leagues/
      page.tsx         ← team-progress period list (week/tier filters) + settings + manual week closure
      [id]/
        page.tsx       ← period members: move tier, adjust progress points, remove, force re-sync
    users/
      page.tsx         ← user list + role management (superadmin only)
    organizations/
      page.tsx         ← tenant registry: create, invite the first admin, suspend/resume, impersonate (Phase 40.9)
    demo-requests/
      page.tsx         ← the demo-request pipeline: list, inline status change, confirm-gated Approve,
                          and (SuperAdmin only) one-click Provision — org + bootstrap invite
                          (docs/DEMO_REQUEST.md, "Provisioning")
```

### The organization panel (`/org/*`, block 40.20)

Routes under `/org` admit `TenancyAdmin`/`TenancySuperAdmin` and platform staff; everyone else is
redirected to `/tree`. Screen-by-screen specifications live in
[TENANCY/ADMIN_UI_DESIGN.md §2](TENANCY/ADMIN_UI_DESIGN.md); the shell that carries them is
`features/org-shell/`.

```
app/(org)/
  layout.tsx           ← gate + sidebar + mobile drawer + ImpersonationBanner + state O0
  org/
    page.tsx           ← O1  Команда: skill heat map + gap panel
    assignments/       ← O2 list, O3 new, O4 one assignment (funnel, waves, reminder)
    dialogs/           ← O5 team conversations, O6 transcript and review
    reviews/           ← O7 disputes and sent coaching notes
    profile/           ← O8 company profile (interview / full form)
    content/           ← O9 three queues; generation/ O10–O11, adaptations/ O12–O13,
                          overrides/ O14–O15, lessons/[lessonId] O19
    people/            ← O16 invitations and members
    usage/             ← O17 AI spend
    program/           ← O18 the learning programme

features/org-shell/
  components/org-sidebar.tsx           ← the nav, structurally identical to the platform one
  components/no-organization-state.tsx ← state O0
  constants/navigation.ts              ← all nine entries; owned by slice 0, read by the rest
  hooks/use-org-nav-badges.ts          ← the three sidebar counters
  hooks/use-team-directory.ts          ← useTeamSkillMap / useTeamMemberNames, shared by four slices
  lib/legacy-admin-redirects.ts        ← the §1.5 table
```

The sidebar carries the panel's only three counters, each answering "is there work for me there":
active assignments (`GET /admin/assignments?status=active`), open score disputes
(`GET /admin/dialog-reviews?kind=score_dispute&status=open`), and a dot — no number — when any
override has gone stale (`GET /admin/content/overrides?staleOnly=true` **or**
`GET /admin/dialog/overrides/modes?staleOnly=true`). `staleTime` 60s, refetch on window focus. A
failing request contributes nothing rather than a zero or a dot: a dot that means "we could not
ask" sends somebody looking for work that is not there.

---

## UI principles

### Platform panel (`/admin/*`)

- Minimal, functional, monochrome color scheme
- Standard HTML-like forms via Tailwind utility classes
- Tables for list views
- Inline delete confirmation (no separate modal — just a button state change to "Confirm?")
- JSON import sections collapsible on each entity page (Skills, Lessons)

### Organization panel (`/org/*`)

- Everything visible is Russian, «вы»-form. Enum values, query keys, `data-*` and logs stay English
- Built from `shared/components`, not from raw utilities — the customer opens this every week and it
  has to look like the rest of the product. Block 40.20 added seven components for it: `Modal`,
  `ConfirmDialog`, `DataTable`, `EmptyState`, `PageHeader`, `Tabs`, `MetricBar`
- Lime is a fill only: `--primary-ink` for brand-coloured text, `--on-primary` on a lime fill
- "Done" is `--success`, never lime — on a heat map they must read apart at badge size
- Numbers are `--font-mono`, tabular
- One `variant="primary"` button per screen

---

## JSON Import Workflow

JSON import is available inline on Skills and Lessons pages:

1. Click "Import JSON" button
2. Download template for reference (shows all supported fields)
3. Upload your JSON file
4. View import results (created/updated counts, errors)

The **Techniques** page exposes the same trio in its header — **Download template**
(a valid `techniques_template.json` with dialog / case / coach examples),
**Export JSON**, and **Import JSON** — sourced from `TECHNIQUES_TEMPLATE` in
`features/admin/lib/import-templates.ts`. Import upserts by `slug`; leave
`primarySkillId` / `additionalSkillIds` `null` / `[]` in the template and set the
real skill after import, using the skill linking described next.

#### Skill linking (AD-3, 2026-08-21)

Every technique's card on `/admin/techniques` carries a "No skill" / current-skill-title badge
next to a `<select>` — pick a skill and it saves immediately (`PUT /admin/techniques/:id` with
only `primarySkillId` changed), no need to open the full "Edit" form. To link many techniques at
once (the situation AD-3 was found in: all 45 production techniques had no skill at all), check
their row checkboxes — or "Select all visible" to grab everything the current search/skill filter
shows — pick a skill in the toolbar that appears, and click "Assign primary skill to N"; each
selected technique is saved independently, and a failure on one row is reported by name rather than
silently absorbed into the others' success. See `docs/DECISIONS.md` (AD-3) for why this is
skill-level rather than lesson-level, and `docs/API_CONTRACTS.md` for the endpoint reuse. There is
still no UI for `additionalSkillIds` — only `primarySkillId`, which is what both the admin `?skill=`
filter and the public guidebook's skill facet key off.


### Lessons Template (with all 10 exercise types)
```json
[
  {
    "topicIconicName": "cold-calls",
    "title": "Opening the Call",
    "orderInTopic": 1,
    "exercises": [
      {
        "type": "choose_option",
        "orderInLesson": 1,
        "content": {
          "situation": "Клиент говорит: 'Это слишком дорого'",
          "options": [
            { "text": "Да, понимаю. Могу предложить скидку.", "is_correct": false },
            { "text": "Скажите, дорого относительно чего?", "is_correct": true },
            { "text": "Это лучшая цена на рынке.", "is_correct": false }
          ],
          "explanation": "Лучше уточнить причину возражения."
        }
      },
      {
        "type": "fill_blank",
        "orderInLesson": 2,
        "content": {
          "before": "Клиент: У нас уже есть поставщик.",
          "after": "Клиент: Ну, в целом да, можно обсудить.",
          "options": [
            { "text": "Понял, но мы лучше!", "is_correct": false },
            { "text": "А что если я покажу, как сэкономить 20%?", "is_correct": true },
            { "text": "Жаль, до свидания.", "is_correct": false }
          ]
        }
      },
      {
        "type": "reorder",
        "orderInLesson": 3,
        "content": {
          "instruction": "Расставьте этапы холодного звонка",
          "items": [
            { "text": "Приветствие", "correct_position": 1 },
            { "text": "Выявление потребности", "correct_position": 2 },
            { "text": "Презентация", "correct_position": 3 },
            { "text": "Работа с возражениями", "correct_position": 4 }
          ],
          "explanation": "Сначала понять потребность, потом предлагать."
        }
      },
      {
        "type": "match_pairs",
        "orderInLesson": 4,
        "content": {
          "instruction": "Соедините возражение с техникой",
          "pairs": [
            { "left": "Слишком дорого", "right": "Сравнение ценности" },
            { "left": "Нам ничего не нужно", "right": "Техника бумеранга" },
            { "left": "Отправьте на почту", "right": "Техника моста" }
          ],
          "explanation": "Каждое возражение требует своего подхода."
        }
      },
      {
        "type": "categorize",
        "orderInLesson": 5,
        "content": {
          "instruction": "Распределите вопросы по категориям",
          "categories": ["Хороший вопрос", "Плохой вопрос"],
          "items": [
            { "text": "Какие цели на квартал?", "category": "Хороший вопрос" },
            { "text": "Вам нравится наш продукт?", "category": "Плохой вопрос" }
          ],
          "explanation": "Хорошие вопросы открытые и про понимание."
        }
      },
      {
        "type": "spot_mistake",
        "orderInLesson": 6,
        "content": {
          "dialogue": [
            { "speaker": "seller", "text": "Добрый день!", "is_mistake": false },
            { "speaker": "seller", "text": "Мы лучшая CRM!", "is_mistake": true },
            { "speaker": "client", "text": "Нам ничего не нужно.", "is_mistake": false }
          ],
          "explanation": "Питч вместо discovery — ошибка.",
          "ai_prompt": "Оцени понимание проблемы питча."
        }
      },
      {
        "type": "rewrite",
        "orderInLesson": 7,
        "content": {
          "instruction": "Перепишите тему письма цепляюще",
          "original": "Предложение о сотрудничестве",
          "evaluation_criteria": ["Персонализация", "Интрига", "Краткость"],
          "ai_prompt": "Оцени улучшение темы письма."
        }
      },
      {
        "type": "ai_dialogue",
        "orderInLesson": 8,
        "content": {
          "persona": "Скептик Сергей",
          "scenario": "Discovery-звонок",
          "context": "IT-директор, скептичен, торопится",
          "max_turns": 6,
          "success_criteria": ["Качество вопросов", "Работа со скептицизмом"],
          "ai_prompt": "Оцени диалог продавца."
        }
      },
      {
        "type": "evaluate_call",
        "orderInLesson": 9,
        "content": {
          "transcript": [
            { "speaker": "seller", "text": "Здравствуйте, это Алексей." },
            { "speaker": "client", "text": "Добрый день." },
            { "speaker": "seller", "text": "Рассматриваете новые решения?" }
          ],
          "evaluation_axes": [
            { "name": "Квалификация", "description": "Была ли квалификация?" },
            { "name": "Открытые вопросы", "description": "Использовались ли?" }
          ],
          "ai_prompt": "Сравни оценку с анализом звонка."
        }
      },
      {
        "type": "free_text",
        "orderInLesson": 10,
        "content": {
          "situation": "Клиент: 'Это слишком дорого'",
          "instruction": "Напишите ответ на возражение",
          "evaluation_criteria": ["Не снижает цену", "Выясняет причину"],
          "ai_prompt": "Оцени ответ на возражение."
        }
      }
    ]
  }
]
```

---

## Exercise Types (10 total)

| Type | Description | Key Content Fields |
|------|-------------|-------------------|
| `choose_option` | Select best answer from options | situation, options: [{text, is_correct}], explanation |
| `fill_blank` | Fill gap in dialogue | before, after, options: [{text, is_correct}] |
| `reorder` | Arrange items in sequence | instruction, items: [{text, correct_position}], explanation |
| `match_pairs` | Connect left/right columns | instruction, pairs: [{left, right}], explanation |
| `categorize` | Sort items into buckets | instruction, categories[], items: [{text, category}], explanation |
| `spot_mistake` | Identify mistake in dialog | dialogue: [{speaker, text, is_mistake}], explanation, ai_prompt |
| `rewrite` | Improve given text | instruction, original, evaluation_criteria[], ai_prompt |
| `ai_dialogue` | Practice with AI persona | persona, scenario, context, max_turns, success_criteria[], ai_prompt |
| `evaluate_call` | Evaluate transcript quality | transcript: [{speaker, text}], evaluation_axes: [{name, description}], ai_prompt |
| `free_text` | Write based on prompt | situation, instruction, evaluation_criteria[], ai_prompt |

See `src/frontend/lib/exerciseTypes.ts` for TypeScript constants.
See `src/backend/api/Features/Exercises/ExerciseTypes.cs` for C# constants.

---

## Visual Exercise Editor

The admin panel provides a visual editor for all 10 exercise types at:
`/admin/skills/[skillId]/topics/[topicId]/lessons/[lessonId]/exercises`

Features:
- Type-specific form fields for each exercise type
- Drag reordering with up/down arrows
- Inline preview of content
- Add/edit/delete exercises without raw JSON editing
- Auto-assigns orderInLesson based on position

Each exercise type has a dedicated editor component in `src/frontend/features/admin/components/exercise-editors/` (kebab-case):
- `choose-option-editor.tsx` / `multiple-choice-editor.tsx`
- `fill-blank-editor.tsx`
- `ordering-editor.tsx`
- `matching-editor.tsx`
- `categorizing-editor.tsx`
- `find-error-editor.tsx`
- `rewrite-better-editor.tsx`
- `ai-dialog-editor.tsx`
- `rate-call-editor.tsx`
- `open-question-editor.tsx` / `written-answer-editor.tsx`

Each component includes the canonical TypeScript schema and client-side validation.

---

## Ordering Rules

- **Lessons** have `sortOrder` by their position within a skill
- **Exercises** have `sortOrder` by their position within a lesson
- Backend queries always `OrderBy(x => x.SortOrder)` to ensure consistent ordering
- Visual editor allows reordering via up/down arrows

---

## The РОП's content pipeline — API only (Phases 40.27–40.28)

**The screens are 40.20's O9–O11 and land in slice 5.** They are drawn in
[TENANCY/ADMIN_UI_DESIGN.md](TENANCY/ADMIN_UI_DESIGN.md) against these routes; the `/org/*` shell
they hang off shipped in slice 0. What exists behind them already is the whole pipeline under
`/admin/content-generation/*` — see [CONTENT_PIPELINE.md](CONTENT_PIPELINE.md) and
[API_CONTRACTS.md](API_CONTRACTS.md).

What the screen has to do, when it is designed, in the order that matters:

1. **A textarea and a title.** The material is pasted text — a product deck's contents, a call script,
   notes from a training session. File upload and call recordings are 40.30.
2. **A spinner with a poll.** `POST` returns immediately with status `structuring`; the structuring
   call takes tens of seconds to minutes. `GET /admin/content-generation/{jobId}` is the poll.
2a. **The refusal, which is a screen of its own (40.28).** The run may come back `insufficient`
   instead of `awaiting_review` — either immediately, if the pasted text was too thin or not about
   selling at all, or after structuring, if too little could be read out of it. `insufficiency.gaps`
   is a list of `{code, message}`; **render it as a list of bullets, never as a paragraph**, because
   usually only one of them is something the РОП can act on today. Two things this screen must
   offer, and they are the whole reason the refusal is a state rather than an error:
   a textarea that `POST …/material` appends (the run resumes where it stopped and does not re-read
   the deck), and — when a structure exists — the ordinary checkpoint editor, because somebody who
   knows their four objections should be able to type them instead of hunting for a document. Do not
   render a refusal as an error toast: it is the most useful answer the product gives on that path,
   and «добавьте примеры возражений или запись звонка» is worth more to that customer than the
   lesson they asked for would have been.
3. **The checkpoint, which is the whole screen.** Product, who they sell to, tone, the objections,
   the stages of their script, the glossary, the banned claims — every one editable, every one
   deletable, and gaps shown as gaps rather than hidden. «Всё верно? что убрать, что добавить?»
   `PUT …/structure` saves the edit; it is idempotent and can be pressed as often as the reviewer
   likes.
4. **One approve button**, and it should be obvious that it is the expensive one. Everything before it
   costs seconds; after it, the same correction means re-generating a lesson.
5. **The result.** `producedLessonId` names a real archived lesson with real exercises. The screen
   should link into the existing lesson editor rather than growing a viewer of its own — it is an
   ordinary lesson, which is the point.

Three things the screen must not do, because the backend deliberately does not support them:

- **Do not offer a "generate anyway" button on a refused run.** There is no route for it. The
  threshold is answerable — add material, or fill the structure in by hand — but not waivable, and
  every path back through it is re-inspected. A bypass would hand the customer the fifteen bland
  exercises this block exists to not sell them.

- ~~**Do not offer per-exercise accept/reject.** That is roadmap 40.32.~~ **40.32 shipped it, and it
  is a separate screen rather than a step of this one** — see below. A generated lesson still arrives
  archived and un-archiving it is still `PUT /admin/lessons/{id}` with `isArchived: false`; what
  changed is that any stage of any content can now be sent through a proposal queue answered item by
  item. Do not fold that queue into the pipeline's result step: the two have different lifetimes, and
  a run that is `completed` has nothing left to review.
- **Do not write the reviewed structure into the organization profile *yourself*.** It looks like the
  same form — it is the same field list — and it is deliberately a separate draft. Since 40.29 there
  is a route that does it properly, under a merge policy: `POST /organizations/profile/draft/apply`.
  A client that instead assembled a `PUT /organizations/profile` out of the structure would overwrite
  the customer's `bannedClaims` with whatever the deck happened to say, which is the exact failure the
  separation exists to prevent. See below.

## The profile interview — API only (Phase 40.29)

**This is 40.20's O8 and it lands in slice 4.** The backend is four
routes on `/organizations/profile` ([API_CONTRACTS.md](API_CONTRACTS.md#organization-profile-as-an-interview-phase-4029),
[ORGANIZATION_SERVICE.md](ORGANIZATION_SERVICE.md#the-profile-as-an-interview-phase-4029)).

The thing the screen must get right is that **this is not a form with AI assistance; it is an
interview**. What that means concretely:

1. **Never render seven inputs.** `GET /organizations/profile/gaps` returns three questions at a time
   and a `totalGapCount`. Show the three, show «осталось ещё N», and re-fetch after each answer. The
   whole block exists because the seven-field version stays empty.
2. **One answer, one `PATCH`.** Do not read the profile, splice a field in and `PUT` all seven back —
   that loses whatever a colleague saved in between, in exactly the multi-person situation this flow
   invites. `PATCH /organizations/profile` with a single field is the write.
3. **The «заполнить по материалам» path is the 40.27 pipeline, not a new upload box.** The РОП starts
   an ordinary content-generation run with their deck and their script, corrects the structure at the
   checkpoint, and the screen then posts that `structure` to `POST /organizations/profile/draft` (to
   look) and `…/draft/apply` (to commit). One reading of one document fills the profile *and* produces
   a training.
4. **Show the conflicts, and do not pre-tick them.** The preview's `fields[]` carries `decision` per
   field. `fill` and `extend` happen anyway and need no consent — they destroy nothing. `conflict`
   means «есть ваше значение и предложение ИИ», and the field is left alone unless its name is sent
   back in `acceptedFields`. A screen that pre-selected every conflict would be the silent overwrite
   with a checkbox drawn on it.
5. **`isReadyForParameterization` is the progress indicator worth showing**, not «5 из 7 полей». It
   goes true when `product`, `icp` and three objections exist — the point at which lessons stop
   reading as «ваш продукт» and start reading as the customer's own.

Two things this screen must not do:

- **Do not offer a way to delete a banned claim from the draft flow.** There is no such route: apply
  only ever adds to `bannedClaims`. Removing one is a deliberate act on the whole-profile form, by
  somebody looking at the whole list.
- **Do not treat the two «skippable» questions as unanswered work.** `banned_claims` and `glossary`
  may honestly be «таких нет» and the profile has no marker for that, so they persist. They are
  `important` and `optional`, they never appear while a `blocking` gap is open, and they must not hold
  a completion badge hostage.


---

## Batch adaptation and content review — API only (Phase 40.32)

**These are 40.20's O12–O13 and they land in slice 6.** The backend is seven
routes under `/admin/content/adaptations`
([API_CONTRACTS.md](API_CONTRACTS.md), [CONTENT_PIPELINE.md §6a](CONTENT_PIPELINE.md)).

Two screens, sharing everything but the middle column:

- **`mode: "tone_rewrite"`** — «перепиши все упражнения этапа "закрытие" под наш продукт и тон».
- **`mode: "quality_review"`** — «что не так с тем, что мы написали руками».

What the screens have to do, in the order that matters:

1. **Pick a stage, press once.** `POST /admin/content/adaptations {mode, stageKey}` returns
   immediately with the batch and its items, all `pending`. Nothing has been spent yet — the scope is
   a database query. A stage above the per-batch ceiling is a **400 carrying the count**, and the
   right response on screen is «в этапе 412 упражнений, это дорого — сузьте выбор», not a retry.
2. **A progress bar with a poll.** `preparing` means items still owe an AI call; `GET
   …/adaptations/{jobId}` gives `pendingCount` against `itemCount`. Minutes, not seconds: it is one
   call per exercise.
3. **The queue, which is the whole screen.** `awaitingReviewCount` is the number the header should
   show — a batch is not done when the model finishes, it is done when a person has answered every
   proposal. Order by lesson and position, so a reviewer reads a lesson the way it plays.
4. **One item at a time.** `GET …/items/{itemId}` returns the current body, the proposed body and
   `changes` — the list of JSON leaves that differ. Render `changeSummary` (the model's sentence about
   what it changed) **first**: it is what lets somebody answer in five seconds, and the leaf list is
   what they check it against. In review mode the middle column is `findings`, and
   `hasBlockingFinding` is what must sort or badge the list — a queue of sixty advisory notes must not
   bury the one saying the correct answer teaches a forbidden promise.
5. **Accept, reject, next.** Both take an item id. `isStale: true` means the exercise was edited after
   the proposal was computed: disable accept and say so, because the server will 409 anyway and the
   honest fix is a re-run, not a merge.

Four things the screens must not do, because the backend deliberately does not support them:

- **Do not build an «применить всё» button.** There is no route for it, and adding one would be
  auto-apply with the reviewer's name attached — the one thing this block exists to prevent. If the
  queue feels too long, the answer is a narrower stage.
- **Do not offer accept in review mode.** A finding is a diagnosis, not a patch; the route returns
  409. Link to the ordinary exercise editor instead, and to a tone rewrite of the same stage.
- **Do not render a diff you computed yourself.** The server already enumerates which leaves differ,
  and it deliberately never merges the two documents. A client-side three-way merge of prose and
  grading criteria is the exact thing 40.18 refused to build.
- **Do not expect the change to be live.** Accepting edits the draft exercise. Learners see it when
  somebody publishes a new lesson version on the existing 40.15 route — the screen should say so, or
  a РОП will accept forty rewrites and wonder why the team still reads the old wording.
