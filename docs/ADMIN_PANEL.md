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
admin screen itself is roadmap block 40.20, waiting on the owner's design.

---

## Authorization policies (backend)

| Policy | Satisfied by | Applied to |
|---|---|---|
| `RequirePlatformAdmin` | `role` ∈ {`Admin`, `SuperAdmin`} | All `/admin/*` **content** endpoints (learning, ai, gamification, social), the `/organizations` tenant registry, and the read side of `/admin/users` |
| `RequireSuperAdmin` | `role` = `SuperAdmin` | Everything that adds or removes a user platform-wide: `PUT/DELETE /admin/users/*`, plus all of `/admin/platform/*` (impersonation, bootstrap-admin) |
| `RequireOrgAdmin` | `org_role` ∈ {`TenancyAdmin`, `TenancySuperAdmin`} **or** `role` ∈ {`Admin`, `SuperAdmin`} | No call site today — reserved for the organization admin screen (40.20) |
| `RequireOrgSuperAdmin` | `org_role` = `TenancySuperAdmin` **or** `role` = `SuperAdmin` | `/invites`, `/memberships` — adding and removing an organization's users |

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
| POST | /admin/platform/organizations/bootstrap-admin | identity-service | invite the organization's first `TenancySuperAdmin` |
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

**There is no screen for any of this yet, and that is 40.20.** The admin-panel split — a platform
superadmin panel and an organization panel — is waiting on the owner's design, and every block from
40.15 onward has shipped its backend without a frontend for the same reason. What did ship on the
manager's side of 40.25 is `/dialog-reviews` (their inbox) and the dispute link in the dialog
feedback modal; those are ordinary app screens, not admin ones.

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
  layout.tsx           ← sidebar nav + auth guard
  admin/
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
```

---

## UI principles

- Minimal, functional, monochrome color scheme
- Standard HTML-like forms via Tailwind utility classes
- Tables for list views
- Inline delete confirmation (no separate modal — just a button state change to "Confirm?")
- JSON import sections collapsible on each entity page (Skills, Lessons)

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
real skill after import.


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
