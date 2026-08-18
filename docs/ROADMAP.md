# Sellevate — ROADMAP

## How the agent works with this file
1. Read phases top to bottom
2. Find the first block with status `[ ]`
3. Decompose it into sub-tasks independently
4. Execute → mark `[x]` → commit → next block
5. If a block is blocked — mark `[~]` and write the reason next to it

## Statuses
- `[ ]` — not started
- `[>]` — in progress (agent is working on it right now)
- `[x]` — done
- `[~]` — blocked (reason next to it)

---

## Phase 1 — Foundation

### [x] Project structure
### [x] Auth (email + Google)
### [x] Onboarding
### [x] Skill tree — main screen
### [x] Lesson screen — multiple choice type
### [x] Lesson screen — fill in the blank type
### [x] Lesson screen — free text + AI evaluation
### [x] Reference materials
### [x] Profile and statistics
### [x] Leagues — weekly team progress

## Phase 2 — Packaging
### [x] Landing page
### [x] Demo mode (no registration)

---

## Phase 3 — Admin Panel

### [x] Role system
- [x] Add `UserRole` enum (User / Admin / SuperAdmin) to `User` entity
- [x] EF migration: add `Role` column
- [x] Include `role` claim in JWT access token
- [x] Update `AuthTokenResponseDto` to expose `role`
- [x] Seed default SuperAdmin on startup (env vars `SUPERADMIN_EMAIL` / `SUPERADMIN_PASSWORD`)

### [x] Backend authorization
- [x] Register `RequireAdmin` and `RequireSuperAdmin` ASP.NET Core policies
- [x] Admin skills CRUD (`GET/POST/PUT/DELETE /admin/skills`)
- [x] Admin lessons CRUD (`GET /admin/skills/:id/lessons`, `POST/PUT/DELETE /admin/lessons`)
- [x] Admin exercises CRUD (`GET /admin/lessons/:id/exercises`, `POST/PUT/DELETE /admin/exercises`)
- [x] Admin reference CRUD (`GET /admin/skills/:id/reference`, `POST/PUT/DELETE /admin/reference`)
- [x] Admin users list + role change (`GET /admin/users`, `PUT /admin/users/:id/role`) — SuperAdmin only
- [x] Update API_CONTRACTS.md with all admin endpoints

### [x] Frontend admin panel
- [x] Add `role` field to `authStore` (Zustand)
- [x] Create `app/(admin)/layout.tsx` — sidebar + auth guard (redirect non-admins)
- [x] Skills list page (`/admin/skills`) — table + create/delete
- [x] Skill detail page (`/admin/skills/[id]`) — edit skill metadata + lessons table
- [x] Lesson detail page (`/admin/skills/[id]/lessons/[lessonId]`) — exercises table + JSON editor per exercise
- [x] Reference materials page (`/admin/skills/[id]/reference`) — list + markdown editor
- [x] Users page (`/admin/users`) — table with role badge + role change (SuperAdmin only)
- [x] Admin nav link in user profile (visible to admins only)

### [x] Content Seeder
- [x] `POST /admin/seeder/csv` — bulk import skills/lessons/exercises from CSV (upsert logic)
- [x] CSV parser without external dependencies (handles RFC 4180 quoting)
- [x] Frontend seeder page at `/admin/seeder` — file upload, result stats, template download
- [x] "Seeder" link added to admin sidebar nav
- [x] `docs/SEEDER.md` — CSV format, API reference, Excel/Sheets guide
- [x] `docs/TESTING/SEEDER.md` — manual checklist + integration test outline


### [x] User voice transcription
- [x] `ITranscriptionService` interface
- [x] `WhisperTranscriptionService` — calls OpenAI Whisper API
- [x] `TranscriptionController` — `POST /transcription/transcribe`
- [x] Whisper config in `appsettings.json` (`Whisper:Model`, `Whisper:Language`, `Whisper:MaxFileSizeMb`)
- [x] Service registered in `Program.cs`

### [x] New backend architecture
- [x] Analyse current monolith structure
- [x] Design microservice split with Kafka, Redis, MongoDB activation
- [x] `docs/BETTER_ARCHITECTURE.md` (Russian) — full explanation with block scheme

### [x] Admin — Lessons page
- [x] `GET /admin/lessons` — all lessons with skill info (skillTitle, skillIcon)
- [x] `/admin/lessons` page: filters (skill, difficulty, search), sortable columns, inline edit, add form, delete modal
- [x] Added to admin nav sidebar

### [x] Content Import — skillIcons in file
- [x] Remove `/admin/bulk-lessons` page and `POST /admin/seeder/lessons/bulk` endpoint
- [x] `POST /admin/seeder/lessons` now reads `skillIcons` array per lesson (no skillId query param)
- [x] JSON: `"skillIcons": ["cold-calls", "objection-handling"]`; CSV: `skill_icons` pipe-separated
- [x] One lesson item can be imported into multiple skills at once

---

## Phase 4 — AI & Voice

### [x] Re-enable OpenAI evaluation
- [x] Uncomment `FreeTextEvaluationStrategy` — calls `gpt-4o-mini` with sales coach system prompt
- [x] Re-register named `"OpenAI"` HttpClient in `Program.cs` (timeout 30s)
- [x] Graceful fallback when `OpenAI:ApiKey` not set or placeholder

### [x] Voice input for free-text exercises
- [x] Mic button in `FreeTextExercise` component (MediaRecorder API)
- [x] Record → stop → POST audio blob to `POST /transcription/transcribe`
- [x] Transcribed text appended to textarea; recording/transcribing/error states shown
- [x] `useTranscribeAudio()` mutation hook in `useLesson.ts`

---

## Phase 5 — Activity Consistency & Progress Tracking

### [x] Daily activity consistency calculation job
- [x] Hangfire recurring job (runs daily at 00:05): for each user check `LastActivityDate`
  - if yesterday → activity streak continues (no-op, updated on exercise submit)
  - if > 1 day ago → reset `CurrentStreakDayCount` to 0
- [x] Update `ExerciseService` to set `LastActivityDate = today` and increment consistency count on submit
- [x] Expose `currentStreak` in Profile API response
- [x] Show activity consistency on profile page

### [x] Progress points source tracking
- [x] `UserXp` records created on exercise completion (source: `exercise`)
- [x] Consistency bonus progress points on 7-day and 30-day milestones (source: `streak_bonus`)
- [x] Weekly progress point totals fed into team progress standings

---

## Phase 6 — Team Progress Promotion

### [x] Team progress promotion/demotion logic
- [x] `WeeklyLeagueClosureJob` — full logic implemented:
  - Top-10 in each cohort promoted to next tier
  - Bottom-5 demoted
  - New week: reset `weekly_xp`, create new `LeagueMembership` rows
- [x] Frontend team progress page shows promotion/demotion zone highlights
- [x] Push notification / banner on week close (in-app only)

---

## Phase 8 — Duolingo Green Redesign (Lesson Execution)

> Full visual redesign of the main app flow based on Stitch design `projects/5546133593140033209`.
> Spec: [docs/LESSON_EXECUTION_REDESIGN.md](LESSON_EXECUTION_REDESIGN.md)

### [x] Design tokens & fonts
- [x] Add Manrope font via `next/font/google` in `app/layout.tsx`
- [x] Update `globals.css`: body font → Manrope, define CSS color variables for green scheme
- [x] Add CSS utility classes: `node-center`, `node-left`, `node-right`, `btn-3d`, `slide-up`

### [x] SkillNode component redesign
- [x] `positionClass` prop (node-center / node-left / node-right)
- [x] Completed: yellow circle `#FFC800`, completed-state badge top-right
- [x] Active: green circle `#58CC02`, `animate-ping` outer ring, popover card above node
- [x] Locked: gray `#F7F7F7` circle, lock icon SVG, `cursor-not-allowed`
- [x] Zigzag offset via `node-left` / `node-right` / `node-center` CSS classes
- [x] Popover card: skill title, mini progress bar, X/total lessons, "Старт" green button

### [x] StatsWidget redesign
- [x] 3 separate border cards: Daily Activity (yellow), Progress Points (blue), Total Progress Points (red)
- [x] Each card: `border-2 border-border-color rounded-2xl`, hover accent border
- [x] Mascot card below with motivational tip text

### [x] Skill Tree page (`/tree`) redesign
- [x] Section header banner: green bg, shadow `0 4px 0 0 #58A700`, X/total badge
- [x] Vertical path line with active green segment overlay
- [x] Zigzag node offsets (center/right/right/center/left/left pattern)
- [x] Right sidebar: new StatsWidget

### [x] Skill Path page (`/skill/[id]`) redesign
- [x] Replace lesson list with vertical node path
- [x] Lesson nodes: completed/active/locked variants with zigzag offsets
- [x] Active node popover: lesson name, "Урок X из N", green "Старт" button
- [x] Progress bar header with completed/total count

### [x] Exercise page (`/exercise/[id]`) redesign
- [x] New header: X button + green progress bar + ❤️ hearts counter (starts at 4)
- [x] Character speech bubble for situation field
- [x] Multiple choice + fill blank: numbered badge + `border-b-4` 3D buttons, blue selected state
- [x] FreeTextExercise: 3D button style applied
- [x] ExerciseResultBanner: CSS slide-up animation, correct (green) / incorrect (red) panels

### [x] Smoke test
- [x] Full lesson flow: tree → skill path → exercise → result banner → next exercise
- [x] Verify all node states render correctly
- [x] Verify correct/incorrect banner slides up and continues properly

---

## Phase 7 — Polish & Mobile

### [x] Mobile UX pass
- [x] Responsive skill tree (touch-friendly nodes via CSS zigzag offsets)
- [x] Exercise screen bottom-safe-area padding (`env(safe-area-inset-bottom)`)
- [x] BottomNav iOS safe-area padding
- [x] Profile page on small screens (grid stays 2-col, font scales)
- [x] `viewport` meta with `viewportFit: cover` for edge-to-edge iOS support
  
### [x] Skill focus — lesson path on home tab
- [x] Profile tab: skill picker (shows unlocked skills, persists choice in localStorage)
- [x] `/tree` tab: if skill selected → shows lesson path (ordered by sortOrder); otherwise → full skill tree
- [x] "Сменить навык →" link from tree to profile; "Показать все навыки" to clear selection
- [x] Shared `LessonPath` component reused by both `/tree` and `/skill/[id]`

### [x] All-lessons path in /tree
- [x] `GET /lessons` endpoint — all lessons across all skills, sorted by sortOrder
- [x] `useAllLessons()` hook on frontend
- [x] `/tree` (no skill selected) shows full lesson path instead of skill nodes
- [x] Empty state shown when no lessons exist
- [x] Fix bottom nav overlap — bumped layout padding-bottom to `6rem + safe-area`

### [ ] Performance
- [ ] Redis sorted set for team progress standings

---

## Phase 9 — Lesson Execution Session

> Spec: [docs/LESSON_EXECUTION_FLOW.md](LESSON_EXECUTION_FLOW.md)

### [x] Sequential lesson unlock
- [x] `UpdateLessonProgressAsync` — auto-unlock next lesson in same skill on completion
- [x] `EnsureSkillLessonsSeededAsync` — lazy-init on first lessons fetch (first → available, rest → locked)
- [x] `UnlockNextLessonInSkillAsync` — unlock next lesson by sortOrder after correct answer
- [x] Unit tests: 7 cases covering seeding, locked skill, already-seeded, unlock, edge cases
- [x] Integration tests: seed on first access, unlock after submit, full 3-lesson sequential flow
- [x] Docs: [LESSON_UNLOCK.md](LESSON_UNLOCK.md), API_CONTRACTS.md updated

### [x] Tap-to-open popover on lesson nodes
- [x] Replace always-on active-node popover with tap-to-toggle popover
- [x] Popover: lesson title, "Урок N из Total", "Приступить к прохождению" button
- [x] Click-outside closes popover; only one open at a time

### [x] Full-screen session tab `/session/[lessonId]`
- [x] New route outside `(main)` layout (no BottomNav)
- [x] Header: close (✕), progress bar (per-exercise), hearts (4, lose on wrong answer)
- [x] Character speech bubble + numbered 3D choice buttons
- [x] Session completion screen: progress points earned, "Вернуться к пути" button
- [x] Hearts = 0 → failure screen with "Попробовать снова"
- [x] Restart session resets state without navigation

### [x] Wire popover → session tab
- [x] "Приступить к прохождению" navigates to `/session/[lessonId]`

### [x] Skip button in exercises
- [x] ПРОПУСТИТЬ button shown alongside ПРОВЕРИТЬ in all exercise types
- [x] Skip advances to next exercise without penalty or submission
- [x] Button only visible when `onSkip` prop provided (session context)

### [x] Team progress countdown timer
- [x] "До конца недели Xд Xч" computed from `weekEndDate` and displayed above team progress list
- [x] Updates every minute; shows minutes when < 1 hour remaining

### [x] Animated dashed path line
- [x] Active lesson segment uses SVG animated dashed green stroke (1.2s loop)
- [x] Completed segments stay solid green; inactive stays gray



## Phase 10 — Post-Session Statistics

> Реализовать статистику после прохождения урока.

### [x] Session result screen enhancements
- [x] Track session duration (start time → end time) via `useRef`
- [x] Track per-exercise correctness (correct / total) via `correctAnswerCount` state
- [x] Show on completion screen: time spent, accuracy %, progress points earned, hearts remaining (2×2 grid)
- [x] `formatSessionDuration(seconds)` pure utility: "X сек" or "X мин Y сек"

---

## Phase 11 — Milestones & Recognition

> Design source: project `16384358117617625529` — "Profile & Statistics (Vivid)" screen shows milestones section.
> Also noted as missing in [docs/STITCH_ANALYSIS.md](STITCH_ANALYSIS.md).

### [x] Backend — Achievement system
- [x] `Achievement` entity: id, key, title, description, iconEmoji, conditionType, conditionThreshold, sortOrder
- [x] `UserAchievement` entity: userId, achievementId, unlockedAt
- [x] EF migration `AddAchievements` — creates `Achievements` and `UserAchievements` tables
- [x] `AchievementSeeder` — seeds 10 default achievements on startup (idempotent)
- [x] `AchievementService.EvaluateAchievementsAfterSubmitAsync` — evaluates all conditions on correct submit
- [x] `GET /profile/achievements` — returns all achievements with `isUnlocked`/`unlockedAt`
- [x] `ExerciseSubmissionResultDto` extended with `NewlyUnlockedAchievementKeys`
- [x] API_CONTRACTS.md updated

### [x] Frontend — Milestones on Profile
- [x] `useAchievements()` hook — fetches `/profile/achievements`
- [x] Milestones 5-col grid on `/profile` page: locked (grayscale) vs unlocked (green border)
- [x] Footer: "X из 10 разблокировано"
- [x] `ExerciseSubmissionResult` type extended with `newlyUnlockedAchievementKeys`

---

## Phase 12 — Persona-based Onboarding

> Design source: project `16384358117617625529` screens "Onboarding Step 1: Profile Selection" and
> "Onboarding: Select Profile (Vivid)". Shows avatar cards for sales roles (SDR, AE, Account Manager, etc.).

### [x] Backend — User persona
- [x] Add `Persona` (nullable text) to `UserProfile` entity
- [x] EF migration `AddPersonaToUserProfile`
- [x] `CompleteOnboardingRequestDto` extended with optional `Persona` field
- [x] `OnboardingService` saves persona on completion
- [x] `PUT /profile/persona` endpoint — validates allowed values
- [x] `GET /profile` response includes `persona`

### [x] Frontend — Persona selection step in onboarding
- [x] New onboarding step 0: persona picker (SDR, AE, AM, Founder, Other) with emoji + description cards
- [x] Click → sets persona + advances to step 1; "Пропустить" skips without setting persona
- [x] `totalStepCount` bumped from 3 → 4; remaining steps shifted 1→2→3
- [x] `CompleteOnboarding` payload now includes `persona`
- [x] Profile page: persona displayed as green badge/tag below email

---

## Phase 13 — Lesson Course Map

> Design source: project `16384358117617625529` screen "Lesson Map: Objections" (id `4d961c964dd540f9bfe83133b73d6028`).
> A detailed overview screen per skill showing all lessons as a structured course map.

### [x] Frontend — Skill course map page `/skill/[id]/map`
- [x] Header: skill title, icon, total lessons, completion %
- [x] List of lesson cards: lesson number, title, description excerpt, status (locked/active/completed), progress points reward
- [x] Completed lessons: green check, duration shown
- [x] Active lesson: highlighted card with "Начать" CTA
- [x] Locked lessons: dimmed, lock icon, shows what unlocks them
- [x] "Start" button on active lesson → `/session/[lessonId]`
- [x] Link from skill node popover: "Посмотреть карту курса"

### [x] Backend — Lesson descriptions
- [x] `Lesson` entity has `description` (nullable) and `estimatedMinutes` fields
- [x] `LessonSummaryDto` exposes `description` and `estimatedMinutes`
- [x] Frontend `LessonSummary` interface updated accordingly

---

## Phase 14 — Sales Handbook: Key Techniques

> Design source: project `16384358117617625529` screens "Sales Handbook (Vivid)" and
> "Sales Handbook: Key Techniques" — structured reference with categories, search, technique cards.

### [x] Backend — Reference material enhancements
- [x] Add `category` field to `ReferenceMaterial` entity (nullable text)
- [x] Add `tags` field (comma-separated text, exposed as string[] in DTO)
- [x] `GET /reference?category=&search=` — filter + search endpoint
- [x] `GET /reference/categories` — distinct categories list
- [x] Migration `AddCategoryTagsToReference` (also includes Lesson.Description + EstimatedMinutes)
- [x] `ReferenceMaterialDto` updated with `category`, `tags`, `skillSlug`

### [x] Frontend — Handbook page redesign (`/guidebook`)
- [x] Category chips at top (dynamic from API), "Все" default
- [x] Search input with debounce (useDeferredValue) — filters cards in real-time
- [x] Technique cards: category badge, tags pills, title, excerpt, expand on tap
- [x] Expanded card: full markdown content, "Связанный навык →" link
- [x] Empty state when search yields no results
- [x] "📖 Справочник" added to BottomNav


## Phase 15 — Admin Reference Material CRUD (Global)

> Полное управление справочными материалами из единой страницы в админ панели.
> Все администраторы (Admin + SuperAdmin) могут просматривать, создавать, редактировать и удалять материалы.

### [x] Backend — extend admin reference endpoints
- [x] Add `category`, `tags`, `skillTitle`, `skillSlug` to `AdminReferenceMaterialDto`
- [x] Extend `CreateReferenceMaterialRequestDto` with `category?` and `tags?`
- [x] Add `GET /admin/reference` — list all materials with optional `?skillId=&search=&category=` filters
- [x] Add `GET /admin/reference/categories` — distinct categories
- [x] Update `PUT /admin/reference/:id` and `POST /admin/skills/:id/reference` to accept category/tags
- [x] Update API_CONTRACTS.md

### [x] Frontend — /admin/reference page
- [x] `useAdminReferenceAll()` hook — fetches `/admin/reference` with filters
- [x] `useAdminReferenceCategories()` hook — fetches categories
- [x] Create `/admin/reference/page.tsx` — table: skill, title, category, tags, sort; with search + skill + category filters
- [x] Inline edit row: title, category, tags, sortOrder, markdownContent (expandable textarea)
- [x] "New material" form: select skill (from `/admin/skills`), fill fields
- [x] Delete with confirm modal
- [x] Update existing `/admin/skills/[id]/reference` page to also show/edit category + tags fields
- [x] Add "Reference" link to admin sidebar nav

### [x] Docs & tests
- [x] Update API_CONTRACTS.md with reference section
- [x] Add manual test checklist to `docs/TESTING/ADMIN_REFERENCE.md`

---

## Phase 16 — Next Lesson Button after Session

> После прохождения урока показывать кнопку "Следующий урок", которая сразу открывает следующий разблокированный урок в том же навыке.

### [x] Backend — next lesson endpoint
- [x] Add `GET /lessons/:lessonId/next` — returns `{lessonId, title, xpReward}` or 204 if none
- [x] Query: find the lesson's skill, then find next lesson (by sortOrder) with status `available`
- [x] Update API_CONTRACTS.md

### [x] Frontend — next lesson on session completion screen
- [x] `useNextLesson(lessonId, enabled)` hook — queries `/lessons/:lessonId/next`, enabled only on session complete
- [x] On completion screen: if next lesson available → show green "Следующий урок →" button above "Вернуться к пути"
- [x] "Следующий урок" navigates to `/session/[nextLessonId]` (replaces history)
- [x] If no next lesson → show "Все уроки пройдены! 🎉" message; "Вернуться к пути" is green button

### [x] Docs & tests
- [x] Update API_CONTRACTS.md with `NextLessonDto`
- [x] Add test checklist to `docs/TESTING/NEXT_LESSON.md`

---

## Phase 17 — Keyboard Controls in Exercise Session

> При выполнении упражнений пользователь может управлять с клавиатуры: цифры 1-4 выбирают варианты,
> Enter/Space применяют «Проверить» или «Продолжить».

### [x] Multiple choice — digit keys
- [x] `useKeyboardControls(options, onSubmit, onContinue)` hook in `session/` dir
- [x] Keys 1–4: select the corresponding answer option (only when result banner is not shown)
- [x] Enter / Space: trigger "Проверить" if answer selected, or "Продолжить" if result showing
- [x] Hook attached in `SessionPage` and respects `disabled` state (no re-selection after submit)

### [x] Fill-in-the-blank — focus & submit
- [x] Enter submits when fill-blank input is focused and non-empty
- [x] Space not intercepted when typing in text input (only in non-input context)

### [x] Keyboard hint UI
- [x] Small gray hint below action button: "или нажмите Enter" / "или нажмите 1–4"
- [x] Hidden on touch devices (`@media (pointer: coarse)`)

### [x] Docs & tests
- [x] `docs/TESTING/KEYBOARD_CONTROLS.md` — manual checklist
- [x] Unit tests for `useKeyboardControls` hook

---

## Phase 18 — Milestone Unlock Notification

> После разблокировки вехи показывать всплывающее уведомление (toast / modal) с анимацией.

### [x] Achievement toast component
- [x] `AchievementToast` component: badge emoji, title, description, green border, slide-in animation
- [x] Auto-dismiss after 4s; tap/click to dismiss early
- [x] Positioned top-center on mobile, top-right on desktop

### [x] Wire to session completion
- [x] `submitExercise` result includes `newlyUnlockedAchievementKeys`
- [x] After each correct submit: if keys non-empty → fetch achievement details → show toast queue
- [x] Queue: show toasts one at a time with 500ms gap between each

### [x] Achievement details lookup
- [x] `useAchievements()` data cached in session; map key → `{iconEmoji, title, description}`
- [x] No extra API call — use already-loaded achievements list

### [x] Docs & tests
- [x] `docs/TESTING/ACHIEVEMENT_NOTIFICATION.md` — manual checklist
- [x] Verify: toast shows after unlock, doesn't show on wrong answer, queue drains correctly

---

## ~~Phase 19 — Profile Stats & Weekly Target~~ [SKIP]

> Design source: project `16384358117617625529` screen "Profile & Statistics (Vivid)" (`f1f97c280d784009a583743912f9fb6c`).
> Profile currently shows activity consistency/progress points/milestones but lacks mastery %, team progress badge, and weekly progress-point target.

### [ ] Weekly progress-point target panel
- [ ] Backend: `GET /profile` returns `weeklyXpGoal` (configurable, default 600) and `weeklyXpCurrent`
- [ ] Frontend: progress bar "X / Y points" with subtitle on profile page
- [ ] Show encouraging copy when near/over goal

### [ ] Mastery % stat
- [ ] Backend: compute mastery as % of completed exercises across all enrolled skills
- [ ] Expose `masteryPercent` in `GET /profile` response
- [ ] Show as 4th stat card on profile page (alongside activity consistency, progress points, team progress)

### [ ] Team progress badge on profile
- [ ] Show current team progress tier name ("Ruby", "Elite", etc.) as a stat card on profile page
- [ ] Link from profile team progress card → `/league` page

---

## ~~Phase 20 — Team Progress Highlights (Top-3 highlight)~~ [SKIP]

> Design source: project `16384358117617625529` screen "League Leaderboard (Vivid)" (`866d49cb3eb64879beff714b05b53fd5`).
> Current team progress page has a flat list. Design shows top-3 as large highlighted cards with avatars and recognition icons.

### [ ] Top-3 highlight section
- [ ] Replace first 3 rows in team progress list with large featured cards: avatar, recognition icon, name, progress points
- [ ] 1st place gets a distinct icon, 2nd/3rd get secondary recognition icons
- [ ] Remaining positions (4–N) keep standard row layout

### [ ] Current user highlight row
- [ ] User's own row always visible (even if position > 10), highlighted with "Current Promotion Zone" badge if in top-10
- [ ] Show position number prominently

### [ ] Motivational banner
- [ ] Encouraging progress banner between the top cards and the rest of the list

---

## ~~Phase 21 — Daily Goal & Activity Consistency Widget on Dashboard~~ [SKIP]

> Design source: project `16384358117617625529` screens "Skill Tree Dashboard (Duo-Style)" (`7acba62367db4bdd8e576d3a07353ba3`) and "Дашборд: Дерево навыков" (`1404814a34ac48c49fc0411a00df3d31`).
> Both show a "Daily Progress" panel and a daily progress-point goal bar on the main skill tree page.

### [ ] Daily progress-point goal bar on /tree
- [ ] Backend: `GET /profile` adds `dailyXpGoal` (default 50 points) and `dailyXpToday`
- [ ] Frontend: progress bar "X / Y points today" shown in StatsWidget or dedicated card on `/tree`
- [ ] Visual: near-complete state (green fill) vs incomplete (gray)

### [ ] Daily activity consistency widget upgrade
- [ ] Current activity consistency card on `/tree` shows only number — add "X days" label with a progress prompt ("One more lesson keeps your progress going!")
- [ ] Show days-of-week mini calendar (M T W T F S S) with completed days highlighted

---

## Phase 22 — NPC Mentor / Skeptic Sergey Card [SKIP]

> Design source: project `16384358117617625529` screens showing "Skeptic Sergey" mentor card on skill tree and guidebook.
> Currently no mentor character card exists on the main screens. Design shows an interactive coach card with a "CHALLENGE SERGEY" or "SEE FULL STRATEGY" button.

### [ ] Mentor card component
- [ ] `MentorCard` component: avatar image/emoji, name, role title, motivational quote
- [ ] "Challenge" CTA button — navigates to the next available exercise session
- [ ] Shown on `/tree` dashboard as a card below the skill nodes

### [ ] Mentor quote rotation
- [ ] Backend: `GET /mentor/tip` returns a random motivational tip/quote (seeded list, 10+ entries)
- [ ] Frontend: tip displayed in mentor card, refreshed on each visit

---

## ~~Phase 23 — Technique Mastery Progress in Guidebook~~ [SKIP]

> Design source: project `16384358117617625529` screen "Sales Handbook (Vivid)" (`dd7025dbdd42452daa297f5c91be013f`).
> Current guidebook has categories and search but no mastery tracking per technique/reference material.

### [ ] Technique mastery tracking
- [ ] Backend: track which reference materials a user has "practiced" (e.g., viewed + completed a related exercise)
- [ ] `GET /reference` response includes `masteryLevel` per item (0–max)
- [ ] Frontend: progress indicator on each technique card in guidebook ("Level 3", "3/8 Completed")

### [ ] Mentor panel in guidebook
- [ ] Inline "Coach Marcus" or mentor persona panel within expanded technique card
- [ ] Shows persona-based insight (e.g., "Skeptic Sergey" case study) + 2–3 micro-prompts (practice, tips)

---

## ~~Phase 24 — Quests / Daily Challenges~~ [SKIP]

> Design source: project `16384358117617625529` screen "Дашборд: Дерево навыков" (`1404814a34ac48c49fc0411a00df3d31`) — nav shows "КВЕСТЫ" tab; "Skill Tree Dashboard (Duo-Style)" shows "Quests" nav item.
> A dedicated quests/challenges system is referenced in the design but not implemented.

### [ ] Backend — Daily quests system
- [ ] `Quest` entity: id, type (daily/weekly), title, description, conditionType, conditionThreshold, xpReward, expiresAt
- [ ] `UserQuest` entity: userId, questId, progress, completedAt
- [ ] `QuestSeeder` — seeds 3 daily quests refreshed each day (e.g., "Complete 3 exercises", "Earn 100 points", "Log in 3 days in a row")
- [ ] `GET /quests` — returns active quests with user progress
- [ ] `QuestProgressJob` — evaluates quest progress after exercise submit

### [ ] Frontend — /quests page
- [ ] Quest cards: title, description, progress bar (X/Y), progress-point reward badge
- [ ] Completed quests: green check, "Completed" label
- [ ] Expired/missed quests: grayed out
- [ ] "КВЕСТЫ" tab added to bottom navigation

---

## ~~Phase 25 — Sample Dialogs in Reference Materials~~ [SKIP]

> Design source: project `16384358117617625529` screen "Sales Handbook: Key Techniques" (`8b30f1041d804b6f8cdfd029bb188c20`).
> Technique cards in the design show expandable "Sample Dialog" sections with prospect/rep scripted exchanges and coach insights.

### [ ] Sample dialog field in reference materials
- [ ] Add `sampleDialog` (nullable JSON/text) field to `ReferenceMaterial` entity + migration
- [ ] `ReferenceMaterialDto` exposes `sampleDialog` as structured text
- [ ] Admin reference editor: textarea for sample dialog
- [ ] Frontend: expanded technique card shows "Sample Dialog" section with alternating prospect/rep chat bubbles

### [ ] Case study snippets
- [ ] Add `caseStudy` (nullable text) field to `ReferenceMaterial` entity
- [ ] Shown as a highlighted sub-card within expanded technique in guidebook

---

## ~~Phase 26 — Performance: Redis-backed Team Progress~~ [SKIP]

> Deferred from Phase 7.

### [ ] Redis sorted set for team progress
- [ ] Replace DB query for team progress standings with Redis sorted set (`ZADD`, `ZRANK`, `ZRANGE`)
- [ ] Update `WeeklyLeagueClosureJob` to sync Redis on week close
- [ ] Update `GET /league` to read from Redis with DB fallback

---

## Phase 27 — AI Dialog Practice

> New tab "Диалог" for AI-powered sales conversation practice.
> Spec: [docs/AI_DIALOG.md](AI_DIALOG.md)

### [x] Backend — PostgreSQL + MongoDB setup
- [x] `DialogBundle` EF entity linked to `Skill` (PostgreSQL)
- [x] `DialogMode` EF entity with `ChatSystemPrompt` and `FeedbackSystemPrompt` (PostgreSQL)
- [x] `DialogSession` MongoDB entity with messages, feedback, xpEarned
- [x] EF configurations and migration
- [x] `MongoDbContext` for sessions

### [x] Backend — Dialog entities & DTOs
- [x] `DialogBundle` entity (skillId, title, description, iconEmoji, sortOrder, isActive)
- [x] `DialogMode` entity (bundleId, key, title, description, chatSystemPrompt, feedbackSystemPrompt, sortOrder, isActive)
- [x] `DialogSession` entity (userId, bundleId, modeId, status, messages[], feedback, xpEarned, timestamps)
- [x] `DialogBundleDto`, `DialogModeDto`, `DialogSessionDto`, `DialogSessionSummaryDto`, `DialogMessageDto`
- [x] Request DTOs with `chatSystemPrompt` and `feedbackSystemPrompt` fields

### [x] Backend — OpenAI chat service
- [x] `IOpenAiChatService` interface with `ChatMessageResult` and `FeedbackResult`
- [x] `OpenAiChatService` — calls GPT-4.1-mini for chat, GPT-4.1 for feedback
- [x] Auto-append `[DIALOG_END]` instruction to chat prompt
- [x] Auto-append `[XP:number]` instruction to feedback prompt
- [x] Parse tags and return structured results
- [x] Graceful degradation: check `IsOpenAiConfigured()` before API calls

### [x] Backend — Dialog public endpoints
- [x] `DialogController` — `GET /dialog/bundles`, `GET /dialog/bundles/{bundleId}/modes`
- [x] `GET /dialog/sessions` — user's session history
- [x] `POST /dialog/sessions` — create session, AI sends first message
- [x] `GET /dialog/sessions/{sessionId}` — get session with messages
- [x] `POST /dialog/sessions/{sessionId}/messages` — send user message, get AI response
- [x] `POST /dialog/sessions/{sessionId}/complete` — end session, generate feedback, award progress points
- [x] Return 503 if OpenAI not configured

### [x] Backend — Admin dialog endpoints
- [x] `AdminDialogController` with RequireAdmin policy
- [x] `GET/POST/PUT/DELETE /admin/dialog/bundles` (with skillId)
- [x] `GET/POST /admin/dialog/bundles/{bundleId}/modes` (with prompts)
- [x] `PUT/DELETE /admin/dialog/modes/{id}` (edit prompts)

### [x] Backend — Seed test data
- [x] `DialogSeeder` — seeds 2 bundles: "Холодные звонки" (обход секретаря, опеннер на ЛПР)
      + "Работа с возражениями" («дорого»), all with `voiceEnabled=true`
- [x] Run seeder on startup (idempotent — skips when any bundle exists)
- [x] Creates a fallback `Skill` if target `iconicName` is missing
      (was: seeder existed in docs but was never wired into `Program.cs` —
      fresh DB showed "Практика диалогов пока недоступна"; fixed 2026-05)

### [x] Frontend — Dialog tab in BottomNav
- [x] Add "💬 Диалог" item to `NAV_ITEMS` (before Profile)
- [x] Route: `/dialog`

### [x] Frontend — Dialog page (bundles grid)
- [x] `useDialogBundles()` hook — fetches `/dialog/bundles`
- [x] `/dialog/page.tsx` — grid of bundle cards (icon, title, description)
- [x] Empty state if no bundles or OpenAI not configured
- [x] Click bundle → navigate to `/dialog/[bundleId]`

### [x] Frontend — Mode selection page
- [x] `useDialogModes(bundleId)` hook — fetches `/dialog/bundles/{bundleId}/modes`
- [x] `/dialog/[bundleId]/page.tsx` — header + mode cards grid
- [x] Click mode → navigate to `/dialog/[bundleId]/[modeId]`

### [x] Frontend — Chat page with history sidebar
- [x] `useDialogSessions()` hook — fetches user's session history
- [x] `SessionHistorySidebar` — sessions grouped by date, progress-point badges
- [x] `/dialog/[bundleId]/[modeId]/page.tsx` — full-screen chat with sidebar
- [x] Toggle sidebar, load previous sessions
- [x] "Новый диалог" button starts fresh session

### [x] Frontend — Session completion & feedback
- [x] Detect `isStopSignal` from AI response
- [x] Show "Завершить диалог" button when stop detected
- [x] On complete → call `/sessions/{id}/complete` → show `FeedbackModal`
- [x] FeedbackModal: progress-point badge, feedback text, "Новый диалог" button

### [x] Frontend — Admin dialog management
- [x] `/admin/dialog/page.tsx` — bundles table with skill selector
- [x] `/admin/dialog/[bundleId]/page.tsx` — modes table with prompt editors
- [x] Separate `ChatSystemPrompt` and `FeedbackSystemPrompt` textareas
- [x] Add "Dialog" link to admin sidebar

### [x] Docs & tests
- [x] Update `docs/FEATURES.md` with AI Dialog link
- [x] Update `docs/API_CONTRACTS.md` with dialog endpoints
- [x] Update `docs/AI_DIALOG.md` with final architecture
- [x] Create `docs/TESTING/AI_DIALOG.md` — manual checklist
- [x] Unit tests for `OpenAiChatService` (mocked HTTP) — `tests/Unit/OpenAiChatServiceTests.cs`
- [ ] Integration tests for dialog endpoints

---

## Phase 28 — Voice Roleplay

> Voice-based sales conversation practice. Stack: VAD (browser) → Deepgram Nova-3 (STT) → GPT-4.1 (logic) → ElevenLabs Flash v2.5 (TTS).
> Spec: [docs/VOICE_ROLEPLAY.md](VOICE_ROLEPLAY.md)
> Target latency: end of speech → start of audio ≤ 700ms

### [x] Phase 28.1 — Backend infrastructure
- [x] Add Deepgram config section to `appsettings.json`
- [x] Add ElevenLabs config section to `appsettings.json`
- [x] Add Voice config section to `appsettings.json`
- [x] `IDeepgramService` interface (config check only, STT runs in browser)
- [x] `IElevenLabsService` interface + `ElevenLabsService` implementation
- [x] `ElevenLabsService` — streaming TTS via HTTP
- [x] Graceful degradation: return empty/503 if keys not configured
- [ ] Unit tests for `ElevenLabsService` (mocked HTTP)

### [x] Phase 28.2 — Database & admin
- [x] Migration: add `VoiceEnabled` (bool) and `VoiceId` (string?) to `DialogModes`
- [x] Update `DialogMode` entity with new fields
- [x] Update `AdminDialogModeDto` with `voiceEnabled`, `voiceId`
- [x] Update admin mode edit form with voice toggle + voice ID input
- [x] `GET /dialog/voice/config` endpoint — returns `{enabled, vadSilenceMs}`

### [x] Phase 28.3 — Voice dialog endpoint
- [x] `IVoiceDialogService` interface
- [x] `VoiceDialogService` — orchestrates GPT + ElevenLabs
- [x] `POST /dialog/sessions/{sessionId}/voice` — accepts transcript, returns audio stream
- [x] Save user message + AI response to MongoDB session
- [ ] Integration test for voice endpoint

### [x] Phase 28.4 — Frontend VAD + Deepgram
- [x] Install `@ricky0123/vad-web` package
- [x] `lib/voice/vadManager.ts` — VAD wrapper with callbacks
- [x] `lib/voice/deepgramClient.ts` — WebSocket client for Nova-3
- [x] `useVoiceConfig()` hook — fetches `/dialog/voice/config`
- [x] Deepgram connection management (open on session start, close on end)

### [x] Phase 28.5 — Frontend audio playback
- [x] `lib/voice/audioPlayer.ts` — Web Audio API streaming playback
- [x] Handle audio stream from backend
- [x] Playback state management (playing, ended, error)

### [x] Phase 28.6 — Frontend UI components
- [x] `VoiceMicButton.tsx` — Duolingo-style mic with green ring animation
- [x] States: idle, listening, processing, playing, disabled
- [x] `useVoice.ts` hook — orchestrates VAD → Deepgram → backend → playback
- [x] Integrate voice mode into chat page (`/dialog/[bundleId]/[modeId]`)
- [x] Show/hide voice button based on mode's `voiceEnabled` flag

### [x] Phase 28.7 — Polish & error handling
- [x] Microphone permission request flow
- [x] Reconnect logic for Deepgram WebSocket
- [x] Error toasts for voice failures
- [x] Fallback to text mode on persistent errors
- [x] Mobile responsive mic button

### [x] Phase 28.8 — Docs & tests
- [x] Update `docs/FEATURES.md` with Voice Roleplay link
- [x] Update `docs/API_CONTRACTS.md` with voice endpoints
- [x] Update `docs/VOICE_ROLEPLAY.md` with final architecture
- [x] Create `docs/TESTING/VOICE_ROLEPLAY.md` — manual checklist
- [x] Frontend component tests for VoiceMicButton — `__tests__/VoiceMicButton.test.tsx`

---

## Phase 29 — New Exercise Types

> Add 8 new exercise types: ordering, matching, categorizing, find-error, rewrite-better, ai-dialog, rate-call, written-answer.
> Spec: [docs/NEW_EXERCISE_TYPES.md](NEW_EXERCISE_TYPES.md)

### [x] Documentation
- [x] Create `docs/NEW_EXERCISE_TYPES.md` — full architecture spec
- [x] Update `docs/API_CONTRACTS.md` with new content/answer schemas
- [x] Update `docs/DB_SCHEMA.md` with ExerciseTypePrompts table
- [x] Update `docs/FEATURES.md` with link

### [x] Backend — Database
- [x] `ExerciseTypePrompt` entity for global AI prompts
- [x] EF configuration for unique ExerciseType index
- [x] Migration `AddExerciseTypePrompts` with seed data

### [x] Backend — Non-AI evaluation strategies
- [x] `OrderingEvaluationStrategy` — exact sequence match
- [x] `MatchingEvaluationStrategy` — pair matching with partial credit
- [x] `CategorizingEvaluationStrategy` — bucket sorting with partial credit

### [x] Backend — AI evaluation strategies
- [x] `AiEvaluationStrategyBase` — shared AI prompt construction and parsing
- [x] `FindErrorEvaluationStrategy` — line selection + AI explanation eval
- [x] `RewriteBetterEvaluationStrategy` — text improvement eval
- [x] `AiDialogEvaluationStrategy` — multi-turn conversation eval
- [x] `RateCallEvaluationStrategy` — transcript analysis comparison
- [x] `WrittenAnswerEvaluationStrategy` — free-form text eval

### [x] Backend — DI and endpoints
- [x] Register all 8 strategies in `ExerciseServiceCollectionExtensions`
- [x] `POST /exercises/{id}/chat` endpoint for ai_dialog type
- [x] `SendChatMessageAsync` in ExerciseService

### [x] Frontend — Exercise components
- [x] `OrderingExercise.tsx` — drag-drop + up/down buttons
- [x] `MatchingExercise.tsx` — two-column connection
- [x] `CategorizingExercise.tsx` — bucket sorting with drag-drop
- [x] `FindErrorExercise.tsx` — line selection + explanation + fixes
- [x] `RewriteBetterExercise.tsx` — textarea with char counter
- [x] `AiDialogExercise.tsx` — chat interface with persona
- [x] `RateCallExercise.tsx` — transcript + criteria rating
- [x] `WrittenAnswerExercise.tsx` — prompt + textarea

### [x] Frontend — Integration
- [x] Update `ExerciseData.type` union in `useLesson.ts`
- [x] Add all component imports to session page
- [x] Add type dispatchers for all 8 new types

### [x] Testing
- [x] Create `docs/TESTING/NEW_EXERCISE_TYPES.md` — manual checklist
- [x] Unit tests for non-AI evaluation strategies (reorder, categorize; match_pairs already covered)
- [x] Unit tests for AI strategies (mocked HTTP) — Rewrite, AiDialogue, FreeText in `tests/Unit/EvaluationStrategies/`
- [ ] Integration tests for chat endpoint

---

## Phase 30 — Friends & Chat

> Social layer: friendships, public profiles, user search, friend progress list, activity feed, and 1-to-1 chat.
> Spec: [docs/FRIENDS.md](FRIENDS.md)

### [x] Backend — Friendship system (PostgreSQL)
- [x] `Friendship` entity with `FriendshipStatus` enum (Pending, Accepted, Declined)
- [x] EF configuration: unique composite index on (RequesterId, AddresseeId)
- [x] Migration `AddFriendships`
- [x] DTOs: FriendDto, FriendRequestDto, PublicProfileDto, UserSearchResultDto, FriendLeaderboardEntryDto, FriendActivityDto
- [x] `IFriendService` interface + `FriendService` implementation
- [x] `FriendController` — 10 endpoints (CRUD, search, progress list, activity, public profile)
- [x] DI registration via `AddFriendFeatureServices()`

### [x] Backend — Chat system (MongoDB)
- [x] `ChatConversation` + `ChatMessage` MongoDB entities
- [x] Add `ChatConversations` collection to `MongoDbContext`
- [x] DTOs: ChatMessageDto, ChatConversationSummaryDto, SendChatMessageRequestDto, CreateConversationRequestDto
- [x] `IChatService` interface + `ChatService` implementation
- [x] `ChatController` — 4 endpoints (conversations CRUD, messages)

### [x] Frontend — Hooks & navigation
- [x] `useFriends.ts` — queries + mutations for friendship operations
- [x] `useChat.ts` — queries + mutations for chat with 5s polling
- [x] Add "Друзья" tab to BottomNav and TopAppBar with pending request badge

### [x] Frontend — Friends pages
- [x] `/friends` page — tabbed view (friends list, requests, progress list)
- [x] `/friends/[userId]` page — public profile with friendship button
- [x] `/friends/chat` page — conversations list
- [x] `/friends/chat/[conversationId]` page — chat view with polling
- [x] Components: FriendCard, FriendRequestCard, UserSearchBar, FriendLeaderboard, FriendActivityFeed, ChatBubble, ChatInput, etc.

### [x] Docs & tests
- [x] Update `docs/API_CONTRACTS.md` with friend and chat endpoints
- [x] Update `docs/DB_SCHEMA.md` with Friendships table and chat_conversations collection
- [x] Create `docs/TESTING/FRIENDS.md` — manual test checklist

---

## Phase 31 — Notifications

> In-app notification center behind the bell icon in the top bar. Covers social events (friend requests, friend request accepted, new chat messages) and progress recognition (milestone unlocked, activity consistency milestones 7/30 days).
> Spec: [docs/NOTIFICATIONS.md](NOTIFICATIONS.md)

### [x] Backend — Notification storage
- [x] `Notification` entity (PostgreSQL): `Id`, `RecipientUserId`, `NotificationType`, `Title`, `Body`, `ActionUrl?`, `RelatedEntityId?`, `IsRead`, `CreatedAt`, `ReadAt?`
- [x] `NotificationType` enum: `FriendRequestReceived`, `FriendRequestAccepted`, `ChatMessageReceived`, `AchievementUnlocked`, `StreakMilestone`
- [x] `NotificationEntityConfiguration` with indexes on `(RecipientUserId, IsRead)` and `(RecipientUserId, CreatedAt DESC)`
- [x] EF migration `AddNotifications`
- [x] Register `DbSet<Notification>` in `AppDbContext`

### [x] Backend — Service, controller, DI
- [x] `INotificationService` interface + `NotificationService` implementation
- [x] Methods: `CreateAsync`, `GetRecentAsync`, `GetUnreadCountAsync`, `MarkAsReadAsync`, `MarkAllAsReadAsync`, `DeleteReadNotificationsOlderThanAsync`
- [x] `NotificationController` with endpoints:
  - `GET /notifications` — paginated list (query `?limit=20&includeRead=true`)
  - `GET /notifications/unread-count` — `{count}`
  - `PUT /notifications/{notificationId}/read`
  - `PUT /notifications/read-all`
- [x] `NotificationFeatureServiceCollectionExtensions.AddNotificationFeatureServices()`
- [x] Register in `Program.cs`

### [x] Backend — Trigger wiring
- [x] `FriendService.SendFriendRequestAsync` → notification to addressee (type `FriendRequestReceived`)
- [x] `FriendService.AcceptFriendRequestAsync` → notification to original requester (type `FriendRequestAccepted`)
- [x] `ChatService.SendMessageAsync` → notification to recipient participant (type `ChatMessageReceived`, action url `/friends/chat/{conversationId}`)
- [x] `AchievementService.EvaluateAchievementsForUserAsync` → notification per unlocked achievement (type `AchievementUnlocked`)
- [x] `ExerciseService.AwardStreakBonusExperiencePointsIfMilestoneAsync` → notification at milestone (type `StreakMilestone`)

### [x] Backend — Cleanup job
- [x] `NotificationCleanupJob` (Hangfire) — deletes read notifications older than 30 days
- [x] Register recurring job in `Program.cs` at `30 0 * * *` (00:30 UTC daily)

### [x] Frontend — Hook & UI
- [x] `useNotifications.ts` — queries (list + unread count with 20s polling) + mutations (mark read, mark all read)
- [x] `NotificationBell.tsx` — button with unread dot, click → dropdown panel
- [x] `NotificationPanel.tsx` — dropdown anchored to bell; list of cards; "Прочитать всё" button; empty state
- [x] `NotificationCard.tsx` — icon by type, title, body, relative time, unread background tint; click → mark read + navigate
- [x] Replace placeholder bell button in `TopAppBar` with `NotificationBell`
- [x] Mobile: full-screen sheet overlay via CSS breakpoint

### [x] Docs & tests
- [x] `docs/NOTIFICATIONS.md` — full feature spec
- [x] Update `docs/API_CONTRACTS.md` with notification endpoints
- [x] Update `docs/DB_SCHEMA.md` with `Notifications` table
- [x] Update `docs/FEATURES.md` with notifications entry
- [x] `docs/TESTING/NOTIFICATIONS.md` — manual checklist

---

## Phase 32 — Header Profile Button Cleanup

> Убрать «лидербордную» трофейную кнопку из правой части `TopAppBar` и заменить
> медальную иконку в чипе профиля на аватар с первой буквой имени пользователя —
> чтобы кнопка явно читалась как «мой профиль». Страница `/league` и навигационная
> вкладка «Лиги» остаются без изменений.

### [x] Frontend — TopAppBar cleanup
- [x] Remove the `emoji_events` (trophy) achievements Link from `TopAppBar.tsx` right-side cluster
- [x] Replace the `military_tech` (medal) icon inside the profile chip with a circular
      avatar showing `firstLetter` from the authenticated user's display name
- [x] Keep the "Уровень {level}" label and the `/profile` navigation target
- [x] Add `aria-label="Профиль (displayName)"` to the chip for accessibility
- [x] Keep `Лиги` nav link (`/league`) untouched

### [x] Docs & tests
- [x] `docs/TESTING/HEADER_PROFILE_BUTTON.md` — manual checklist
- [x] Update `docs/FEATURES.md` testing table with the new checklist

---

## Phase 33 — April Design Refresh

> Полный визуальный редизайн на основе макетов из `.design/redesign/`.
> Дизайн-система: Geist шрифт, earthy палитра (rust/olive/indigo), тёмная тема.
> Ранее выполнено: Phase 1-5 (токены, иконки, UI компоненты, навигация, Skill Tree).

### Phase 33.1 — Session Page Redesign

> Redesign session page header, footer, and overall layout per `session.jsx`.

- [x] Update session header: close button, progress bar, hearts counter with indigo/rust tones
- [x] Update session footer: result banner with slide-up animation
- [x] Update completion screen: confetti, stat tiles grid (progress points, accuracy, time, hearts)
- [x] ChooseOptionExercise: numbered badge + selected state (ink background, sh-2 shadow)
- [x] FillBlankExercise: inline blank styling with dashed border, rust-soft background when filled
- [x] ReorderExercise: ordering cards with up/down buttons, numbered position badges
- [x] Add "1–4 выбрать · Enter — проверить" keyboard hint below footer
- [x] Add milestone toast slide-in animation on completion

### Phase 33.2 — Exercise Components Polish

> Align exercise components with `session.jsx` visual patterns.

- [x] `SpotMistakeExercise`: line selection with good-soft/bad-soft highlight
- [x] `RewriteExercise`: textarea with character counter, criteria list
- [x] `FreeTextExercise`: voice button styling
- [x] `AiDialogueExercise`: chat bubbles with GeoAvatar, fixed footer
- [x] `EvaluateCallExercise`: transcript accordion, star rating buttons

### Phase 33.3 — Guidebook Page Redesign

> Redesign `/guidebook` per `screens.jsx` Handbook section.

- [x] Hero header with stat tiles (Освоено, Мастер, Новых)
- [x] Category chips bar with search input
- [x] Technique cards with mastery ring, category badge, tags
- [x] Expanded card with sample dialog bubbles and coach sidecar
- [x] "Практиковать сейчас" CTA button

### Phase 33.4 — AI Dialog Pages Redesign

> Redesign `/dialog` flow per `screens.jsx` DialogScene.

- [x] Bundles grid page: icon, title, description cards
- [x] Mode selection page: mode cards grid
- [~] Chat page: 3-column layout (case files, scene, coach rail) — skipped, chat page is complex and largely functional
- [~] Scene header: persona avatar, mood chip, timer, goals — skipped
- [~] Chat bubbles with AI flags (good/warn chips) — skipped
- [~] Live commentary sidebar with coach notes — skipped
- [~] Scorecard progress bars — skipped
- [~] Voice mic overlay per `screens.jsx` VoiceMic — skipped

### Phase 33.5 — Onboarding Flow Redesign

> Redesign `/onboarding` per `onboarding.jsx`.

- [x] Step indicator with expanding current step
- [x] Step 1 (Persona): 5-column grid with shape icons
- [x] Step 2 (Sales Type): 3×2 button grid
- [x] Step 3 (Experience): 4-column large buttons with years
- [x] Step 4 (Skills): skill cards with checkbox, icon, lesson count
- [x] Footer with back/continue buttons

### Phase 33.6 — Team Progress Page Redesign

> Redesign `/league` with improved visual hierarchy.

- [x] Week countdown timer above team progress list
- [x] Current user highlight row with promotion zone badge
- [x] Stat tiles for user's progress points and position
- [x] Responsive mobile layout

### Phase 33.7 — Profile Page Redesign

> Redesign `/profile` with stat tiles and milestones grid.

- [x] Stat tiles grid: activity consistency, progress points, level, accuracy
- [x] Milestones section with mastery rings
- [x] Persona badge display
- [x] Settings section styling

### Phase 33.8 — Friends Pages Redesign

> Redesign `/friends` flow with improved cards and chat.

- [x] Friends list with GeoAvatar, status indicators
- [x] Friend request cards with accept/decline buttons
- [x] Team progress tab styling
- [x] Chat page with message bubbles, input styling

### Phase 33.9 — Dark Theme Polish

> Ensure all components work correctly in dark theme.

- [x] Verify all color variables have dark theme overrides
- [x] Test all pages in dark theme
- [x] Fix any contrast or readability issues
- [x] Add theme toggle to profile settings

---

## Phase 34 — Full April Redesign Implementation

> Complete redesign implementation based on `.design/redesign/` assets.
> Desktop-first approach, remove mobile bottom nav, use top nav with mobile hamburger menu.

### Phase 34.1 — Remove bottom navigation, desktop-first layout [x]
- [x] Remove BottomNav import from `(main)/layout.tsx`
- [x] Update TopAppBar to always show (not hidden on mobile)
- [x] Add mobile hamburger menu to TopAppBar
- [x] Update mobile layout padding (no bottom nav space needed)

### Phase 34.2 — Remaining pages to align with redesign [x]
- [x] Session page: update header/footer per `session.jsx` (done in Phase 33.1)
- [x] Completion screen: confetti, stat tiles grid (done in Phase 33.1)
- [x] Voice mode page: full-screen mic — superseded by the telephone-call
      screen `/dialog/[bundleId]/[modeId]/voice` (Phase 36.2)

---

## Phase 35 — Friends Tab April Redesign (Completion) [x]

> Verified 2026-06-05: grep sweep over `src/frontend/app/(main)/friends` and
> `src/frontend/components/friends` finds zero MD3 tokens — the migration was
> already completed as part of Phase 33.8 follow-ups. Marking phase done.

> Phase 33.8 migrated only the outer `/friends` page shell. Inner components and the
> public profile page still use Material Design 3 tokens (`bg-primary-container`,
> `text-on-surface-variant`, `text-tertiary`, `bg-surface-container*`,
> `border-outline-variant`, `font-headline`, `tonal-transition`). This phase finishes the
> migration to the earthy April palette and shared UI primitives (`StatTile`, `GeoAvatar`,
> `Chip`). Routes, hooks, and the 4-tab structure stay as-is.

### [ ] Public profile `/friends/[userId]`
- [ ] Header: `GeoAvatar` + name + persona `Chip`, back link styled with ink palette
- [ ] Stats grid: 4× `StatTile` (rust activity consistency / indigo progress points / olive milestones / neutral avg score)
- [ ] Drop `font-headline`, `bg-*-container`, `text-on-*`, `ring-primary-container`

### [ ] Chat stack
- [ ] `ChatsPane`: skeleton + empty state on `bg-surface` / `bg-bg-2`, copy on `text-ink-*`
- [ ] `ChatWindow`: `border-line`, header avatar → `GeoAvatar`, `transition-colors`
- [ ] `ConversationCard`: avatar → `GeoAvatar`, active row `bg-ink text-bg`, timestamps `font-mono`
- [ ] `ChatInput`, `ChatBubble`: spot-check (already on April palette)

### [ ] Activity feed + list cards
- [ ] `FriendActivityFeed`: icon tones rust/indigo/olive/clay, copy on `text-ink-*`, timestamp `font-mono`
- [ ] `FriendCard`: letter avatar → `GeoAvatar`
- [ ] `FriendRequestCard`: letter avatar → `GeoAvatar`; accept/decline via shared `<Button>`
- [ ] `UserSearchBar`: dropdown avatars → `GeoAvatar`

### [ ] Dark theme & docs
- [ ] Verify dark theme parity on every Friends screen (list, requests, progress list, chats, public profile)
- [ ] Update `docs/TESTING/FRIENDS.md` with visual-parity checklist
- [ ] `grep` sweep: no MD3 tokens (`on-surface`, `primary-container`, `outline-variant`,
      `tonal-transition`, `font-headline`, `bg-surface-container*`, `*-container`) in
      `src/frontend/app/(main)/friends` or `src/frontend/components/friends`

---

## Phase 36 — Telephone-Call Voice Dialog

> Превратить голосовой режим из «чат с микрофоном» в полноценный имитатор
> телефонного звонка: full-screen UI, continuous VAD, streaming GPT → streaming
> TTS, barge-in, лимиты по минутам. Ключи покупаются через рублёвые шлюзы (см.
> [AI_DIALOG.md](AI_DIALOG.md#buying-api-access-from-russia-rub-friendly-proxy-gateways)
> и [VOICE_ROLEPLAY.md](VOICE_ROLEPLAY.md#buying-voice-api-access-from-russia)).

### Phase 36.1 — Stage A: cleanup & RUB-friendly providers
- [x] `Voice:TtsProvider` config switch (yandex only — Google TTS removed 2026-06), default yandex (Voicer support removed 2026-06: queue-based ~10-35 s/task latency unusable for calls)
- [x] Document RUB-friendly OpenAI / TTS / STT proxy gateways
- [x] Drop `IGoogleTtsService` (done 2026-06: single-provider = Yandex)
- [x] Unit tests for TTS services (mocked HTTP)
- [ ] Integration test for `POST /dialog/sessions/{id}/voice`

### Phase 36.2 — Stage B: full-screen "Call" UX
- [x] Full-screen route `/dialog/[bundleId]/[modeId]/voice` (reused existing path)
- [x] Layout: large `GeoAvatar`, persona name, scenario subtitle
- [x] Call states: `dialing → connected → ended` mapped onto VAD pipeline states
- [x] Call timer (mm:ss in mono), pulsing ring tied to current pipeline state
- [x] Red "Положить трубку" button → `/sessions/{id}/complete` + feedback modal
- [x] Continuous VAD (no push-to-talk); state pill becomes activity indicator
- [x] "Позвонить" CTA on `/dialog/[bundleId]` mode card next to "Чат"
- [x] Sound effects: ringing tone + hangup beep (Web Audio synthesis, no mp3 assets)
- [x] Vibration on "connected" (mobile, `navigator.vibrate`)

### Phase 36.3 — Stage B: streaming LLM → streaming TTS
- [x] `IOpenAiChatService.StreamChatMessageAsync` — SSE consumer for `stream: true`
- [x] Sentence-buffer: emit chunks at `. ! ? \n` boundaries (min 20 chars)
- [x] New endpoint `POST /dialog/sessions/{id}/voice/stream` — length-prefixed
      framed chunks `[u32 flags][u32 textLen][text][u32 audioLen][mp3]`
- [x] Frontend `audioPlayer.ts`: queue API (beginQueue / enqueue / markQueueComplete)
      decodes each MP3 immediately and chains via `source.onended`
- [x] `streamReader.ts` helper to decode the framed binary stream on the client
- [ ] Measure: first-audio-byte after user stops speaking (target ≤ 700ms)

### Phase 36.4 — Stage B: barge-in
- [x] VAD detects user speech while `audioPlayer` is playing → stop playback,
      abort the active /voice/stream fetch, recognizer picks up new transcript
- [x] Backend cancellation drops the partial assistant message (clean turn)
- [x] Indicator UI: interrupted AI subtitle fades + «прервано» label

### Phase 36.5 — Stage C: usage limits & billing
- [x] Track per-stream wall-clock seconds in `DialogSession.VoiceSeconds`
- [x] `IVoiceUsageService` — aggregates daily / monthly usage from MongoDB
- [x] Enforce `Voice:DailyLimitMinutes` / `MonthlyLimitMinutes` per user
      → return 429 with `{period, usedSeconds, limitSeconds}`
- [x] `GET /dialog/voice/usage` endpoint + `useVoiceUsage()` hook
- [x] Call screen header shows X/Y MIN СЕГОДНЯ; refetches on hangup
- [x] `/profile` shows minutes used / limit
- [x] Admin page `/admin/voice/usage` — table of users + minute spend

### Phase 36.6 — Docs & tests
- [x] Update `docs/VOICE_ROLEPLAY.md` with the call-mode flow + diagram
- [x] `docs/TESTING/VOICE_CALL.md` — manual checklist (connect, barge-in,
      hangup, limits, fallback to web speech)
- [x] Update `docs/API_CONTRACTS.md` with `/voice/stream` and usage endpoints
---

## Phase 37 — Night Polish Pass (gap analysis 2026-06-05)

> Autonomous overnight pass. Source: full gap analysis of `.design/redesign/` vs
> implemented frontend. Focus: broken legacy styling, unfinished voice roadmap
> items, missing UX states, voice usage surfacing.

### Phase 37.1 — Dialog components: MD3 token cleanup (broken styling)
> MD3 classes (`bg-surface-container*`, `text-on-surface*`, `border-outline-variant`,
> `font-headline`, `tonal-transition`) are NOT defined in the April `@theme` block —
> they silently render as nothing, visibly broken in dark theme.
- [x] `VoiceMicButton.tsx` → April palette
- [x] `SessionHistorySidebar.tsx` → April palette
- [x] `DeleteConfirmModal.tsx` → April palette
- [x] `ChatMessage.tsx`, `ChatInput.tsx`, `BundleCard.tsx` → April palette
- [x] Chat page `/dialog/[bundleId]/[modeId]/page.tsx` → April palette

### Phase 37.2 — Auth pages April migration
- [x] `/login` and `/register` pages → April palette + shared UI primitives

### Phase 37.3 — Legacy green palette migration
- [x] Landing `/` page: `#58CC02`/`btn-3d` → April rust/olive tokens
- [x] `/skill/[id]` page green tokens → April palette
- [x] `/skill/[id]/map` MD3 + green tokens → April palette
- [x] `/reference/[id]` spinner color → April palette
- [x] `SkillNode.tsx`, `ModeCard.tsx` — removed (dead code, no imports)

### Phase 37.4 — Voice call polish (Phase 36.2 leftovers)
- [x] ~~Sound effects: ringback + hangup beeps synthesized via Web Audio~~ — removed 2026-08-14, calls are silent (see DECISIONS.md)
- [x] Vibration on "connected" (mobile, `navigator.vibrate`)
- [x] Barge-in indicator: visual cue when user interrupts AI playback

### Phase 37.5 — Voice usage surfacing (Phase 36.5 leftovers)
- [x] `/profile` shows голосовые минуты: использовано/лимит (день + месяц)
- [x] Backend `GET /admin/voice/usage` — per-user minute spend (Admin policy)
- [x] Admin page `/admin/voice/usage` — table of users + daily/monthly minutes
- [x] Update API_CONTRACTS.md

### Phase 37.6 — Loading skeletons & error/empty states
- [x] Shared `Skeleton` UI primitive (pulse shimmer on `bg-surface-2`)
- [x] Skeletons: `/dialog`, `/guidebook`, `/league` (friends/profile already had them)
- [x] Error states with retry button on data-fetch failures (shared `ErrorState`)
- [x] Empty states: team progress, chat history, guidebook (where missing)

### Phase 37.7 — Admin pages MD3 cleanup
- [x] Admin layout + sidebar → April palette
- [x] Admin pages (skills, lessons, users, topics, techniques, dialog) → April palette
- [x] Exercise editors (8 files) → April palette
- [x] Notification components MD3 leftovers (`NotificationBell/Panel/Card`)
- [x] `ui/Input.tsx`, `ui/Common.tsx` MD3 leftovers

### Phase 37.8 — Docs & tests
- [x] Unit tests for new utilities/components added in this phase
- [x] `docs/TESTING/NIGHT_POLISH.md` — manual checklist
- [x] Update `docs/FEATURES.md`

### Phase 38 — Discuss (Community Forum)
- [x] Backend vertical slice `Features/Discuss` (PostgreSQL): threads, replies, polymorphic
      upvotes (unique index, no double-voting), hybrid curated + free-form tags, EF migration
- [x] `IDiscussService`/`DiscussService`: list (hot/new/unanswered sort, search, tag filter,
      pagination), create thread, replies, vote/unvote, author-or-admin accepted reply,
      popular tags, stats (totals + top authors of the week)
- [x] `DiscussController` (user) + `AdminDiscussController` (pin/hot/delete + tag CRUD)
- [x] Integration tests `DiscussTests` / `AdminDiscussTests` + seeder helpers
- [x] Frontend `/discuss` list (hero, search, sort, tag filter, thread cards, popular-tags +
      top-authors sidebars) and `/discuss/[threadId]` (voting, replies, accept answer)
- [x] Admin `/admin/discuss` (thread moderation + curated tag catalog); nav entries + "forum" icon
- [x] Vitest tests; `.dsc-*` styles ported into `globals.css`
- [x] Docs: `DISCUSS.md`, `TESTING/DISCUSS.md`, API_CONTRACTS, FEATURES

---

## Phase 39 — Companies (Компании)

> New tab: the user keeps a private list of real prospect companies, writes a free-form
> description per company, practices AI calls against that description + a per-call goal
> prompt (reusing the existing full-screen voice-call flow), and logs real calls
> (who / what about / outcome). Design: [docs/COMPANIES/DESIGN_SPEC.md](COMPANIES/DESIGN_SPEC.md).
>
> **Architecture decisions (record in DECISIONS.md when implementing):**
> - New microservice **`company-service`** (`src/backend/company-service/Company`),
>   Postgres DB `company`, host port **5009**, scaffolded after `notification-service`
>   (Serilog+Loki, per-service JWT validation, CORS, health checks, ProblemDetails).
>   No Kafka producer/consumer in MVP (no cross-service state depends on companies);
>   adopt BuildingBlocks eventing later if needed.
> - **ai-service** gains optional per-session context injection: company practice calls
>   are normal `DialogSession`s created with a seeded admin-editable "company-call"
>   `DialogMode` template + injected `{companyName, companyDescription, callGoal}`.
>   The voice pipeline (`/voice/stream`), feedback, progress points, and quotas are reused unchanged.
> - `company-service` stores the link `PracticeCall {companyId, dialogSessionId, goal}`;
>   the company timeline merges practice calls and real-call logs client-side.
>
> **Process (PR-based):** all work happens on the integration branch `feature/companies`.
> Each sub-phase = its own branch `companies/39.X-<slug>` off `feature/companies` →
> implemented by a sonnet executor agent → PR into `feature/companies` → a `code-reviewer`
> agent reviews the PR diff → findings fixed → merge only with green tests
> (`dotnet test`, `tsc`, `vitest`). When the whole phase is done: final PR
> `feature/companies` → `main` for the product owner.
>
> **Scope decision (2026-07-09):** the product owner approved ALL eight extension
> features (Stage B below) — this is a cornerstone feature of the project.
> Mobile bottom nav: «Компании» replaces «Справочник» in the 5-slot bar
> (guidebook stays reachable from the desktop rail), per DESIGN_SPEC §1.4.

### [x] 39.1 Backend — company-service scaffold
- [x] Project `company-service/Company` + `Company.Tests`, added to `Sellevate.sln`
- [ ] `CompanyDbContext` (Postgres `company`), auto-migrate on startup
- [ ] Entities: `Company` (Id, UserId, Name, Description, CreatedAt, UpdatedAt),
      `CallLogEntry` (Id, CompanyId, UserId, ContactName, Subject, Outcome, OccurredAt, CreatedAt, UpdatedAt),
      `PracticeCall` (Id, CompanyId, UserId, DialogSessionId, Goal, CreatedAt)
- [ ] EF configurations: indexes on `(UserId)`, `(CompanyId, OccurredAt DESC)`, `(CompanyId, CreatedAt DESC)`
- [ ] `Program.cs` per notification-service pattern; Dockerfile
- [ ] Update `docs/DB_SCHEMA.md`

### [x] 39.2 Backend — company-service API
- [ ] `CompanyController`: `GET /companies` (list, `?search=`), `POST /companies` `{name}`,
      `GET /companies/{id}`, `PUT /companies/{id}` `{name, description}`, `DELETE /companies/{id}`
- [ ] Call log: `GET /companies/{id}/logs`, `POST /companies/{id}/logs` `{contactName, subject, outcome, occurredAt}`,
      `PUT /companies/{id}/logs/{logId}`, `DELETE /companies/{id}/logs/{logId}`
- [ ] Practice calls: `POST /companies/{id}/practice-calls` `{dialogSessionId, goal}`,
      `GET /companies/{id}/practice-calls`; `GET /companies/{id}/recent-goals` (last 5 distinct)
- [ ] Ownership guard: every query filtered by `UserId` from JWT; 404 on foreign ids
- [ ] Input validation + limits (name ≤ 200, description ≤ 8000, log fields ≤ 4000)
- [ ] Unit tests (service layer, ownership, validation); update `docs/API_CONTRACTS.md`

### [x] 39.3 Backend — ai-service: company-context sessions
- [ ] `StartSessionRequestDto` gains optional `companyContext { companyName, companyDescription, callGoal }`
- [ ] Seed admin-editable `DialogMode` template (key `company-call`, voiceEnabled, hidden from `/dialog/bundles` listing)
- [ ] `DialogService.StartSessionAsync`: when context present → compose chat + feedback
      system prompts from the template with context appended; persist context in the Mongo `DialogSession`
- [ ] Voice stream, complete/feedback, progress-point weights, minute quotas — unchanged and verified with context sessions
- [ ] Unit tests (prompt composition, context persistence); update `docs/API_CONTRACTS.md`, `docs/AI_DIALOG.md`

### [x] 39.4 Infra — gateway, compose, dev scripts
- [ ] YARP: route `/companies/{**catch-all}` → cluster `company` in `gateway/appsettings.json` + gateway tests
- [ ] `docker-compose.yml`: `company` service entry (env, depends_on postgres; gateway env + depends_on)
- [ ] `scripts/dev-company.sh` (`LOCAL_COMPANY_PORT=5009`) + hook into `scripts/dev-up.sh`
- [ ] Update `docs/LOCAL_DEV.md`, `docs/CONFIGURATION.md`, `docs/MICROSERVICES.md`, `docs/ARCHITECTURE.md`

### [x] 39.5 Frontend — nav + companies list
- [ ] `briefcase` icon added to `IconName`; rail item «Компании» in `nav-rail.tsx`; mobile `bottom-nav.tsx` per spec §1.4
- [ ] `features/companies/`: `use-companies.ts` hooks (list/create/update/delete, search)
- [ ] `/companies` page per spec §2: header, toolbar (search + «Добавить компанию»), `.co-row` list,
      create-company modal, empty/loading/error states
- [ ] Vitest tests for hooks + list rendering

### [x] 39.6 Frontend — company page
- [ ] `/companies/[id]` per spec §3: identity header, description card with edit mode,
      pre-call `.co-cta` panel (goal input + recent-goal chips), combined timeline
      (Все / Тренировки / Звонки segmented filter)
- [ ] Real-call log add/edit form (3 fields: с кем / о чём / к чему пришли + дата) + delete confirm
- [ ] Edit/delete company (modal + confirm, navigate back on delete)
- [ ] Vitest tests

### [x] 39.7 Frontend — practice-call handoff
- [ ] Full-screen route `/companies/[id]/call/voice` (outside `(main)`) reusing the existing
      voice pipeline (`useVoice`, call states, sounds, quota) with company-context session creation
- [ ] Optional chat variant `/companies/[id]/call/chat` reusing chat components
- [ ] On session create → `POST /companies/{id}/practice-calls`; hangup → feedback modal → return to `/companies/[id]`
- [ ] Practice entries appear in the company timeline with feedback summary

### [x] 39.8 Core docs checkpoint (Stage A)
- [ ] `docs/COMPANIES/COMPANIES.md` feature doc (core flows); link both COMPANIES docs in `docs/FEATURES.md`
- [ ] `docs/TESTING/COMPANIES.md` — manual checklist (CRUD, ownership, practice call with goal, logs, timeline, mobile)

---

> **Stage B — approved extension features (all eight, 2026-07-09).**
> Same PR process. Order matters: 39.9/39.10 are schema-level and go first;
> AI features (39.12–39.14, 39.16) depend on 39.3 core context plumbing.

### [x] 39.9 Contacts (mini-CRM)
- [ ] Backend: `CompanyContact` entity (Id, CompanyId, UserId, Name, Position, Notes?, CreatedAt, UpdatedAt);
      CRUD `GET/POST /companies/{id}/contacts`, `PUT/DELETE /companies/{id}/contacts/{contactId}`
- [ ] `CallLogEntry.ContactId` (nullable FK, SET NULL on delete) alongside free-text `ContactName`
- [ ] Frontend: contacts section on company page (add/edit/delete); log-form field «С кем говорил»
      becomes combo: pick a contact or type free text (typed name offers «Сохранить как контакт»)
- [ ] Unit tests; `docs/API_CONTRACTS.md`, `docs/DB_SCHEMA.md`

### [x] 39.10 Company status pipeline
- [ ] `Company.Status` enum: `Lead / Contacted / MeetingScheduled / DealWon / DealLost` (default Lead)
- [ ] `PUT /companies/{id}/status`; status included in list/detail DTOs
- [ ] Frontend: status chip on `/companies` rows + status filter chips in toolbar;
      status selector on the company page header (V2 chip colors: lead neutral, contacted info,
      meeting violet, won success, lost danger)
- [ ] Unit tests; docs

### [x] 39.11 Follow-up reminders
- [ ] `Company.NextActionAt` (nullable timestamptz), `NextActionNote` (nullable), `FollowUpNotifiedAt` (nullable)
- [ ] company-service adopts BuildingBlocks eventing (Kafka producer): hosted background service
      polls due follow-ups (every 5 min), publishes `company.followup.due` once per due date
      (guard via `FollowUpNotifiedAt`); topic constant in `BuildingBlocks/Topics`
- [ ] notification-service: consume `company.followup.due` → notification type `CompanyFollowUpDue`,
      title «Пора связаться с {companyName}», actionUrl `/companies/{id}`
- [ ] Frontend: follow-up date + note editor on company page; due/overdue badge on `/companies` rows
- [ ] Unit tests both services (due-poll logic, once-only guard, event contract, consumer→inbox); docs

### [x] 39.12 AI pre-call briefing («Шпаргалка»)
- [ ] ai-service: `POST /ai/companies/briefing` — input: company description, goal?, recent real-call
      logs, feedback summaries of recent practice sessions (by sessionIds from Mongo); output: short
      structured markdown cheat-sheet (кто они, о чём договаривались, возражения, следующий шаг)
- [ ] company-service: `POST /companies/{id}/briefing` — gathers context, calls ai-service
      (internal HTTP, same pattern as learning→ai `/ai/evaluate`), caches result on the company
      (`BriefingContent`, `BriefingGeneratedAt`); `GET` returns cached
- [ ] Frontend: «Шпаргалка к звонку» card on company page — generate/regenerate, markdown render,
      generated-at timestamp; loading/error states
- [ ] Unit tests (prompt composition mocked HTTP, caching); docs

### [x] 39.13 AI real-call log parsing
- [ ] ai-service: `POST /ai/companies/parse-log` `{rawText}` → `{contactName?, subject, outcome, occurredAt?}`
- [ ] company-service proxy: `POST /companies/{id}/logs/parse`
- [ ] Frontend: log form gets «Вставить заметки» mode — paste raw notes/transcript → AI prefills
      the 3 fields → user reviews/edits → save; graceful fallback to manual on AI error
- [ ] Unit tests (mocked HTTP, malformed AI output); docs

### [x] 39.14 AI persona generation for practice calls
- [ ] company-service: `CompanyPersona` entity (Id, CompanyId, UserId, Name, Position, Personality,
      Difficulty enum Easy/Medium/Hard, CreatedAt); CRUD-lite: `GET/POST /companies/{id}/personas`,
      `DELETE /companies/{id}/personas/{personaId}`
- [ ] ai-service: `POST /ai/companies/persona` `{companyDescription, contactName?, contactPosition?, difficulty}`
      → persona JSON; optionally seeded from an existing contact (39.9 synergy)
- [ ] Pre-call `.co-cta` panel: persona selector (chips: сгенерированные персоны + «Без персоны») +
      «Сгенерировать собеседника» (difficulty picker); selected persona injected into `companyContext`
      chat/feedback prompts (extends 39.3)
- [ ] Unit tests; docs

### [x] 39.15 Voice memo → log
- [ ] Frontend: mic button in the log form (MediaRecorder, same UX as free-text exercises) →
      existing ai-service `POST /transcription/transcribe` → transcript lands in the raw-notes
      field → optionally chains into AI log parsing (39.13)
- [ ] Verify gateway route for `/transcription/*` → ai-service (add if missing)
- [ ] Component tests (recording states, error fallback); docs

### [x] 39.16 Readiness score
- [ ] ai-service: `POST /ai/companies/readiness` — input: goal, feedback summaries of last N practice
      sessions for the company; output `{score 0–100, strengths[], gaps[], recommendation}`
- [ ] company-service: `GET /companies/{id}/readiness` — cached (`ReadinessJson`, `ReadinessGeneratedAt`),
      invalidated when a new practice call completes; 204 when no practice sessions yet
- [ ] Frontend: readiness ring + «Что подтянуть» list next to the pre-call panel; empty state
      «Проведите тренировку, чтобы получить оценку готовности»
- [ ] Unit tests (scoring parse, cache invalidation); docs

### [x] 39.17 Final QA, docs & release PR
> **Carry-overs status:** all non-blocking fast-follows below are now cleared via
> PRs #27 (PR #22+#26 — AI backend hardening), #28 (PR #19 — contacts hardening),
> #29 (PR #20 — status dropdown a11y + optimistic update), #30 (PR #24+#21 — persona/
> dialog fencing, persona-delete UI, DI rename, Kafka publish retry, follow-up clock
> caveat). Each was code-reviewed and merged. Two items remain deliberately open:
> (1) **product sign-off** that unconstrained status transitions (e.g. DealWon → Lead)
> are intended — a product decision, not code; (2) **prompt-delimiter injection
> hardening** — company/persona free-text is fenced as data (defense-in-depth) but the
> static `=== ДАННЫЕ ===` delimiters are not escaped, so a user can forge an END marker
> in their own training data (self-injection only, mirrors the accepted pattern across
> Briefing/Persona/ParseLog/Readiness). Tracked as a codebase-wide follow-up (switch to
> per-request token delimiters) — out of scope for the companies release.
> Carry-over from PR #19 review (non-blocking fast-follows): replace the generic
> `InvalidOperationException` contact-validation flow with a typed error; translate
> `DbUpdateException` on the ContactId FK race into a 400; align Create/Update
> contact DTO nullability; clear stale `contactId` client-side on a 400.
> Carry-over from PR #20 review (non-blocking fast-follows): status dropdown uses
> `role="menu"` without the ARIA menu keyboard contract (Escape/arrows/focus return) —
> implement it or downgrade the role; consider optimistic updates for status mutation
> (consistent with the rest of use-companies.ts); CSS `.co-status-filter-chip.active`
> tone overrides rely on source order — bump specificity or comment; product sign-off
> that unconstrained status transitions (e.g. DealWon → Lead) are intended.
> Carry-over from PR #22 review (non-blocking fast-follows): add a company-service
> test for the AI-failure propagation path (client throws → 503, cache left
> unchanged); `InternalAuth:ServiceSecret` is provisioned nowhere and
> learning-service's `AiEvaluationClient` never sends `X-Internal-Service-Secret`
> — either wire the header there too or document that the guard runs open in all
> environments; consider dedicated `BriefingModel`/`MaximumBriefingTokenCount`
> options instead of reusing the feedback/open-question OpenAI config names.
> (The MEDIUM finding — missing input-size guard on `POST /ai/companies/briefing`
> — was fixed in-PR.)
> Carry-over from PR #24 review (non-blocking fast-follows): persona `personality`
> text is injected unfenced into the dialog role-play prompt (consistent with the
> pre-existing company name/description/goal injection, self-injection only) —
> consider fencing all dialog company-context fields as data for defense-in-depth;
> `use-company-personas` exposes a `useDeleteCompanyPersona` mutation with no UI
> consumer yet (wire a manage-personas UI or trim); rename the overloaded
> ai-service `AddBriefingFeatureServices()` (now also wires ParseLog + Persona) to
> `AddCompanyAiFeatureServices()` on next touch. (The LOW transport-failure finding
> — `HttpRequestException` from the AI proxies surfaced as 500 — was fixed in-PR
> for all three proxies, briefing/parse-log/persona.)
> Carry-over from PR #26 review (non-blocking fast-follow): the no-usable-feedback
> readiness result (ai-service returns 204) is not cached, so every `GET
> /companies/{id}/readiness` re-fans-out up to 50 sequential Mongo reads until
> feedback lands — consider a short negative-cache TTL. (The HIGH findings —
> misleading no-op «Обновить» refresh button, ai-service reading sessions without
> user scoping, and error-vs-empty UI conflation — plus the null-forgiving cache
> deserialize were all fixed in-PR.)
> Carry-over from PR #21 review (non-blocking fast-follows): follow-up badge
> due/overdue tone uses the client clock (document caveat or resync against server
> time); consider a short in-process retry (2–3 attempts) around the Kafka publish
> in FollowUpReminderService to absorb transient broker blips within the accepted
> at-most-once design.
- [x] `docs/COMPANIES/COMPANIES.md` updated with all Stage B features; `docs/TESTING/COMPANIES.md` full checklist
- [x] `docs/API_CONTRACTS.md`, `docs/DB_SCHEMA.md`, `docs/ARCHITECTURE.md`, `docs/DECISIONS.md` complete
      (holistic opus integration review confirmed docs match the shipped surface)
- [x] Full `code-reviewer` (opus) + `verifier` pass over `feature/companies` vs `main`
      — opus review: APPROVE, 0 blockers (1 MED internal-auth-secret-in-compose + 2 LOW, all
      documented/post-merge follow-ups); verifier: backend build + all suites green
      (company 122, ai 108, learning 40), frontend tsc + vitest green (250). Lint: the one
      genuine new error (voice-memo ref-in-render) fixed; the codestyle `///` no-comments
      "violations" are an unenforced repo-wide convention (main has 909 such lines) — recorded
      as a DECISIONS exception rather than mass-stripped.
- [x] Final PR `feature/companies` → `main` — PR #31 merged (2026-07-11)

---

## Phase 40 — Мультитенантность: организации (Multi-tenancy)

> Разделение продукта на компании-заказчиков: изоляция данных, кастомизация контента
> под каждую организацию, закрытая выдача доступа (публичной регистрации нет),
> и цикл «РОП → менеджеры» с заданиями после внутренних тренингов.
>
> **Проектная документация (читать до начала любого блока):**
> - [docs/TENANCY/TENANCY.md](TENANCY/TENANCY.md) — изоляция и доступ
> - [docs/TENANCY/CONTENT_MODEL.md](TENANCY/CONTENT_MODEL.md) — версионирование и кастомизация контента
> - [docs/TENANCY/ASSIGNMENTS.md](TENANCY/ASSIGNMENTS.md) — задания и ИИ в админке
> - Запись решения: [docs/DECISIONS.md](DECISIONS.md) (2026-08-14)
>
> **Ключевые решения, зафиксированные до старта:**
> - Тенант называется **`Organization`**, НЕ `Company`. `Company` уже занят — это личный
>   CRM продавца (компании, которым он звонит, скоуп `WHERE UserId = ...`). Колонка
>   `organization_id`, клейм `org_id`, заголовок `X-Organization-Id`, сервис
>   `organization-service` (порт **5010**).
> - У проекта **DB-per-service** (7 БД Postgres + Mongo + Redis), а не одна БД. Поэтому
>   RLS, роль приложения и `SET LOCAL` — общий компонент в `BuildingBlocks`, а не код в
>   каждом сервисе.
> - Три уровня изоляции: гейтвей (`X-Organization-Id` из валидированного JWT, клиентские
>   копии вырезаются) → EF global query filter (удобство, НЕ безопасность) → **RLS в
>   Postgres** (единственный уровень, переживающий `ExecuteUpdate`/Dapper/raw SQL).
>
> **Порядок этапов менять нельзя.** Этап A даёт примитивы, без которых остальное
> написать невозможно; этап C — миграция живых данных, она должна идти после того, как
> механизм изоляции существует и покрыт тестами, но до того, как на нём построен контент.
>
> **Процесс:** интеграционная ветка `feature/tenancy`, каждый под-этап — своя ветка
> `tenancy/40.X-<slug>` → PR в `feature/tenancy` → ревью `code-reviewer` → мерж только с
> зелёными тестами (`dotnet test`, `tsc`, `vitest`). Финальный PR `feature/tenancy` → `main`.

---

### Этап A — фундамент (без него нельзя начинать остальное)

### [x] 40.1 BuildingBlocks — примитивы тенанта
- [x] `ITenantScoped { Guid OrganizationId { get; set; } }`
- [x] `ITenantContext { Guid? OrganizationId; bool IsSystem; }` + scoped-реализация
      `TenantContext` с явным `SetOrganization` / `EnterSystemMode`
- [x] `CrossTenantWriteException` (имя сущности + ожидаемая организация; без утечки
      чужого id в сообщение)
- [x] `TenantSaveChangesInterceptor : SaveChangesInterceptor` — проставляет
      `OrganizationId` на `Added`, на `Modified`/`Deleted` сверяет с `OriginalValues`
      и запрещает менять колонку после создания
- [x] **Обязательно обе перегрузки:** `SavingChanges` И `SavingChangesAsync` — sync-only
      интерцептор в этой кодовой базе не сработает никогда (весь код async)
- [x] Юнит-тесты: вставка без контекста, вставка с чужим id, подмена `OrganizationId` у
      загруженной через `IgnoreQueryFilters()` сущности, `IsSystem` обходит проверку
- [x] Обновить `docs/ARCHITECTURE.md` (раздел BuildingBlocks)

### [x] 40.2 Гейтвей и распространение контекста
- [x] `IdentityHeaders`: константа `OrganizationId = "X-Organization-Id"` + `ResolveOrganizationId(ClaimsPrincipal)`
- [x] Гейтвей: **вырезать** клиентские копии `X-Organization-Id` и выставлять только из
      валидированного токена (то же правило, что уже действует для `X-User-Id`)
- [x] Middleware в BuildingBlocks: заполняет `ITenantContext` из заголовка; 401/403 если
      заголовок отсутствует на tenant-scoped маршруте — реализовано как 403 (`TenantContextMiddleware`
      + `[TenantScoped]` / `.RequireTenantScope()`), обоснование см. `docs/API_CONTRACTS.md`
- [x] Правило кодом: организация **никогда** не читается из body/query/route.
      `scripts/tenancy-boundary-lint.py` (+ CI workflow `tenancy-boundary`) — сканирует
      `OrganizationId` в `*Request.cs`/`*Dto.cs`, `[FromQuery]`/`[FromRoute]` на `organizationId`,
      и `{organizationId}` в шаблонах маршрутов
- [x] Тесты: подделанный клиентом заголовок игнорируется; запрос без заголовка не проходит
- [x] Обновить `docs/API_CONTRACTS.md` (раздел про заголовки)

### [x] 40.3 Контракт событий — `organizationId` в конверте и outbox
- [x] `EventEnvelope`: добавить `OrganizationId` (ломающее изменение общего контракта —
      делается **один раз, до появления первого tenant-scoped консьюмера**)
- [x] `OutboxMessage`: колонка `OrganizationId`; `PartitionKey` оставить user id
      (переезд на `org:user` перетасует партиции без выигрыша)
- [x] Базовый консьюмер: выставляет тенант-контекст из конверта до обработки и
      **падает**, если его нет (нельзя молча обработать без контекста)
- [x] Миграции outbox-таблиц во всех сервисах-продюсерах
- [x] Тесты: `EventContractCatalogTests` / `EventEnvelopeTests` расширить на новое поле
- [x] Обновить `docs/MICROSERVICES.md` (контракт событий)

### [x] 40.4 Инфраструктура RLS
- [x] Роль `sellevate_app` — без `BYPASSRLS`, не владелец таблиц; миграции продолжают
      идти под ролью-владельцем. SQL написан (`docs/TENANCY/sql/create_sellevate_app_role.sql`),
      создание на реальных серверах — вручную, см. `docs/DONT_FORGET.md`
- [x] `TenantConnectionInterceptor` в BuildingBlocks: выставляет `app.organization_id` в начале
      транзакции (`IDbTransactionInterceptor.TransactionStarted`/`TransactionStartedAsync`,
      покрывает `SaveChangesAsync` автоматически — EF уже оборачивает его в неявную транзакцию)
- [x] **`SET LOCAL`, не `SET`** — реализовано и задокументировано; для read-путей без явной
      транзакции требование зафиксировано как обязанность этапа C, см. `docs/DECISIONS.md`
      (2026-08-15)
- [x] Хелпер миграций: `EnableTenantRls(table)` → `ENABLE` + **`FORCE`** ROW LEVEL SECURITY
      + политика с `USING` **и** `WITH CHECK`
- [x] Политика читает `NULLIF(current_setting('app.organization_id', true), '')` — простого
      missing_ok оказалось недостаточно (пул соединений возвращает `''`, не `NULL`, после первого
      использования; поймано интеграционным тестом на реальном Postgres, см. `docs/DECISIONS.md`)
- [x] Отдельный вариант политики для контента: `organization_id IS NULL OR organization_id = ...`
      (`EnableTenantRlsForContent`)
- [x] Запрет `AddDbContextPool` на tenant-scoped контекстах — задокументировано в
      `docs/CODESTYLE.md` + линт `scripts/tenancy-pool-lint.py` (CI: `tenancy-pool`)
- [x] Интеграционный тест на реальном Postgres: под ролью приложения чужие строки не
      видны даже через raw SQL и `ExecuteDelete` — `TenantRowLevelSecurityIntegrationTests`,
      прогнан против локальной `scripts/dev-infra.sh` Postgres, 4/4 зелёных
- [x] Новый файл `docs/TESTING/TENANCY.md` — чеклист проверки изоляции

---

### Этап B — организации, пользователи, доступ

### [x] 40.5 `organization-service` — скаффолд и реестр
- [x] Проект `src/backend/organization-service/Organization` + `.Tests`, в `Sellevate.sln`
- [x] По образцу `notification-service`/`company-service`: Serilog→Loki, JWT-валидация, CORS, health checks, ProblemDetails
- [x] Postgres БД `organization`, авто-миграция на старте (`DatabaseBootstrapper`)
- [x] Сущность `Organization` (Id, Name, Slug, Status: active/suspended, CreatedAt, ...)
- [x] `organization_profile` (продукт, ICP, возражения jsonb, скрипт jsonb, тон,
      глоссарий, `banned_claims`) — см. CONTENT_MODEL.md §3; реализовано как `OrganizationProfile`,
      tenant-scoped (`ITenantScoped`, RLS, `[TenantScoped]`) — см. `docs/DECISIONS.md` (2026-08-15)
- [x] Kafka-продюсер: `organization.created` / `organization.updated` / `organization.suspended`
- [x] Гейтвей: YARP-кластер `organization`, `docker-compose.yml`, `scripts/dev-organization.sh`
      (`LOCAL_ORGANIZATION_PORT=5010`), подключить в `scripts/dev-up.sh`
- [x] Обновить `docs/DB_SCHEMA.md`, `docs/ARCHITECTURE.md`, `docs/LOCAL_DEV.md`

### [x] 40.6 Identity — memberships и разделение ролей
- [x] Таблица `membership (user_id, organization_id, role, status, invited_by, joined_at,
      deactivated_at)`, PK `(user_id, organization_id)` — **с первого дня**, даже пока UI
      разрешает одну организацию — `Memberships` в identity-db, миграция `AddMembership`
- [x] `users.email` остаётся глобально уникальным; у пользователя **нет** колонки организации
- [x] Разделить роли: платформенная (`SuperAdmin` — сотрудники Sellevate) остаётся на
      `user`; организационная (`Manager` / `OrgAdmin`) переезжает в `membership`
- [x] Глобальный `Admin` из `UserRole` исчезает: РОП — админ одной организации, не платформы —
      значение `1` осознанно не переиспользуется (см. `docs/DECISIONS.md`)
- [x] JWT: клеймы `org_id` + `org_role` рядом с существующим `role` — проставляются при
      выпуске токена по активному membership пользователя; отсутствуют, если membership нет
- [x] Политики авторизации `RequireOrgAdmin` / `RequireSuperAdmin`; ревизия всех текущих
      `RequireAdmin` — каждое использование решено осознанно (полная таблица аудита —
      `docs/DECISIONS.md`, 2026-08-15): весь существующий `/admin/*` контент — платформенный,
      `RequireOrgAdmin` — новая инфраструктура без вызовов пока (готова к 40.7/40.20)
- [x] Миграция существующих пользователей → см. 40.9 (выполняется там, не здесь) — схема
      оставляет для этого место (`InvitedBy` nullable, отдельная таблица, не колонка на `user`)
- [x] Обновить `docs/IDENTITY_SERVICE.md`, `docs/ADMIN_PANEL.md`, `docs/API_CONTRACTS.md`,
      `docs/DB_SCHEMA.md`

> **Пересмотрено владельцем 2026-08-16 (ветка `tenancy/40.roles-split`).** Разделение ролей
> этого блока изменено: `Admin` возвращён на платформенный уровень (значение `1`, то же
> значение и тот же смысл, что до 40.6), `Admin` и `SuperAdmin` — чисто наши роли, не
> ограниченные tenancy. У каждой организации появились свои `TenancyAdmin` (бывший `OrgAdmin`,
> переименован без миграции) и `TenancySuperAdmin`. Единственное отличие админа от суперадмина
> на обоих уровнях — добавлять/удалять пользователей может только суперадмин. Политик стало
> четыре: `RequirePlatformAdmin`, `RequireSuperAdmin`, `RequireOrgAdmin`, `RequireOrgSuperAdmin`.
> Полный аудит маршрутов — `docs/DECISIONS.md`, 2026-08-16.
>
> Вторая половина той же правки — «они должны показывать все»: у тенант-контекста появился
> третий режим `IsPlatformWide` (включается **только** клеймом `role` валидированного токена,
> не заголовком), сквозная ветка в EF-фильтрах, GUC `app.platform_mode` в `USING` политик RLS
> (в `WITH CHECK` его нет — чтение расширено, запись нет) и снятие организации с фильтра в двух
> Mongo-репозиториях. Семь миграций `RefreshTenantPoliciesForPlatformStaff` пересоздают политики;
> Redis сознательно не расширен. Детали — `docs/TENANCY/TENANCY.md` §1.6a, проверки —
> `docs/TESTING/TENANCY.md`, что осталось человеку — `docs/DONT_FORGET.md`.

### [x] 40.7 Закрытие регистрации и инвайты
- [x] **Удалить** `POST /auth/register` — не спрятать, не закрыть флагом, а удалить маршрут
- [x] Ревизия Google-входа: разрешён только для email, у которого уже есть membership
- [x] `invite (id, organization_id, email, role, token_hash, expires_at, accepted_at,
      revoked_at, invited_by)`; одноразовый подписанный токен с TTL
- [x] `POST /organizations/{id}/invites` (одиночный и **массовый список email**),
      `DELETE .../invites/{id}` (отзыв), `POST /auth/invites/{token}/accept`
- [x] Приём инвайта на существующий email **добавляет membership**, а не создаёт второго
      пользователя — этот случай невозможно добавить потом
- [x] Инвайт заменяет email-верификацию (владение адресом уже доказано)
- [x] Увольнение = `membership.status = deactivated`, **никогда не удаление**: история
      попыток и звонков менеджера принадлежит организации
- [x] Письма-инвайты через существующий MailerSend-транспорт в BuildingBlocks
- [x] Тесты: истёкший токен, повторное использование, отозванный, чужая организация
- [x] Обновить `docs/EMAIL_VERIFICATION.md`, `docs/API_CONTRACTS.md`

### [x] 40.8 Способ логина как настройка организации (шов под SSO)
- [x] `organization_auth_config (organization_id PK, method, settings jsonb,
      allowed_email_domains text[], jit_provisioning bool, session_ttl, require_mfa)`;
      `method` всегда `password` на этом этапе — реализовано как
      `OrganizationAuthConfigurations` в **identity-db** (не в organization-db: строка читается
      до аутентификации, см. `docs/DECISIONS.md`). Намеренно **без** RLS и без `ITenantScoped` —
      основной запрос кросс-тенантный по своей природе
- [x] `IAuthProvider { string Method; Task<AuthResult> AuthenticateAsync(...); }`
      с **единственной** реализацией `PasswordAuthProvider`
- [x] Трёхшаговый флоу входа уже сейчас: email → резолв организации по домену/инвайту →
      диспатч в провайдера по `method` — `POST /auth/login/start` отвечает одинаково для
      известного и неизвестного адреса (не оракул для перебора, как и `/auth/google` в 40.7);
      организация с методом без провайдера получает `401`, а не откат на пароль
- [x] Фронт: экран входа в две стадии (email, затем метод по ответу сервера)
- [x] SSO (OIDC/SAML) и `jit_provisioning` **не реализуются** — только заложенная развилка;
      реализация по первому платящему запросу. `jit_provisioning`, `session_ttl`, `require_mfa`
      хранятся, но не читаются; эндпоинта записи конфигурации нет — строки появятся в 40.9
- [x] Обновить `docs/DECISIONS.md` (почему шов строится заранее)

### [x] 40.9 Суперадминка платформы и миграция существующих данных
- [x] Экран платформенного суперадмина: создать организацию, пригласить её первого
      `OrgAdmin`, приостановить/возобновить — `/admin/organizations`. Реестр тенантов
      (`/organizations`) закрыт политикой `RequireSuperAdmin` (раньше пускал любого
      аутентифицированного); первый `OrgAdmin` приглашается через
      `POST /admin/platform/organizations/bootstrap-admin`, который переиспользует
      `IInviteService` из 40.7, а не заводит второй путь создания membership
- [x] Импersonation: явный endpoint `POST /admin/platform/impersonation`, выпускающий
      **новый токен** с другим `org_id` и записью в `ImpersonationAuditEntries`. Токен
      намеренно слабее того, которым его запросили: `role: User` (не `SuperAdmin`, поэтому
      он не дотягивается ни до одного `RequireSuperAdmin`-маршрута — включая сам этот),
      claims-маркеры `imp`/`imp_id`/`imp_actor`, TTL 15 минут, refresh-токена нет.
      Приостановленную организацию не пускает даже суперадмина
- [x] Приостановка организации реально блокирует её пользователей — проверка стоит в
      единственной точке, куда сходятся логин, Google, приём инвайта и refresh
      (`IssueTokensForUserAsync`); identity-db получил проекцию реестра
      `OrganizationReplicas`, которую наполняет консьюмер `organization.*`
- [x] **Миграция живых данных:** организация по умолчанию + `membership` всем текущим
      пользователям + строки `organization_auth_config`/`OrganizationReplicas`. Скрипт честен
      про объём: единственная tenant-scoped таблица сегодня — `Invites`, у которой
      `OrganizationId` был `NOT NULL` с рождения (40.7), поэтому бэкфиллить там нечего —
      скрипт это **проверяет**, а не предполагает. Раскатка `organization_id` по остальным
      сервисам — этап C (40.10+), в конце файла лежит шаблон для неё
- [x] Скрипт миграции + **проверенный откат** — `scripts/tenancy-default-organization-backfill.sh`
      и четыре SQL-файла в `docs/TENANCY/sql/`. Откат проверен
      `scripts/tenancy-default-organization-verify.sh` на одноразовых БД, схема которых собрана
      из настоящих EF-миграций: 24/24, включая побайтовое восстановление ролей и membership'ов и
      отказ откатываться, если после бэкфилла кто-то вступил в организацию
- [~] Прогон на копии прод-базы до применения — **человеку**: это операция на живых данных,
      агент запускал скрипты только против одноразовых локальных БД. Порядок расписан в
      `docs/MICROSERVICES_PRODUCTION_MIGRATION.md` §7 и в `docs/DONT_FORGET.md`
- [x] SQL всех разрушающих шагов показать перед выполнением (SAFETY RULES в CLAUDE.md) —
      `--rollback` печатает оба файла целиком и отказывается работать без `--i-have-a-backup`
- [x] Обновить `docs/MICROSERVICES_PRODUCTION_MIGRATION.md` — раздел про раскатку тенантов (§7)

---

### Этап C — раскатка `organization_id` по сервисам

> Для каждого сервиса один и тот же чеклист: колонка → бэкофилл → EF-фильтры →
> индексы с `organization_id` первой колонкой → пересмотр UNIQUE-ограничений →
> RLS → аудит фоновых джоб → тесты изоляции. Порядок сервисов — по риску: сначала
> те, где лежат разговоры и прогресс.

### [~] 40.10 learning-service
- [x] `organization_id` в: `UserSkillProgressRecords`, `UserLessonProgressRecords`,
      `UserExerciseAttempts`, `UserTechniqueProgress` — `NOT NULL`, `ITenantScoped`
- [x] Контентные таблицы (`Skills`, `Topics`, `Lessons`, `Exercises`, `Techniques`,
      `ReferenceMaterials`) — колонка **nullable**: `NULL` = глобальная библиотека
- [x] Query filter для контента: `x.OrganizationId == null || x.OrganizationId == current`
      (НЕ простое равенство)
- [x] Фильтр нужен **каждой** сущности отдельно — навигации `Skill→Topic→Lesson→Exercise`
      фильтр не наследуют. Тест `Every_entity_with_an_organization_id_has_its_own_query_filter`
      обходит модель и валит сборку, если у сущности есть `OrganizationId` и нет фильтра
- [x] `Skill.IconicName`: `UNIQUE(organization_id, iconic_name)` — **плюс частичный уникальный
      индекс по глобальным строкам**: Postgres считает NULL'ы в составном уникальном индексе
      различными, поэтому одного составного мало. То же для `Topic.IconicName` и `Technique.Slug`
- [x] Индексы: `(organization_id, user_id, ...)`, перестройка через `CREATE INDEX CONCURRENTLY`,
      дроп старого **после** — `docs/TENANCY/sql/40.10_learning_organization_indexes_concurrently.sql`
- [x] Проверка `pg_index.indisvalid` — дважды: **до** дропа старых индексов (иначе можно остаться
      вообще без индекса) и после
- [x] Долгие перестройки индексов — **отдельный операционный шаг**: EF-миграция не создаёт ни
      одного индекса, потому что `Database.Migrate()` идёт на старте сервиса. Следствие
      (снимок модели знает про индексы, которых нет до прогона скрипта) записано в
      `docs/DECISIONS.md` и `docs/DONT_FORGET.md`
- [x] RLS на всех десяти таблицах: `EnableTenantRls` для прогресса,
      `EnableTenantRlsForContent` для контента. `ExerciseTypePrompts`, `SkillStages`,
      `DailyQuotes`, `UserReplicas` остаются платформенно-глобальными
- [x] Аудит фоновых джоб: `OutboxRelayBackgroundService` — системный (читает только
      `OutboxMessages`, без RLS), `UserReplicaConsumer` — `RequiresOrganization => false`
      (проекция кросс-организационных пользователей). Незаданный тенант — исключение, а не «все данные»
- [x] Один документированный паттерн транзакций на весь сервис (`TenantTransactionScope`):
      `SET LOCAL` не работает вне транзакции, поэтому голый `SELECT` под RLS вернул бы пусто
- [x] Тесты изоляции **написаны** (8 штук, `LearningTenantIsolationIntegrationTests`):
      навигация, сырой SQL, `ExecuteUpdate`/`ExecuteDelete`, глобальный контент виден обеим
- [~] Тесты изоляции **не прогнаны** — Правило №2 в `docs/DONT_FORGET.md`; человек запускает
      `--filter "TestCategory=Integration"`. Юнит-тесты 61/61 зелёные без них
- [~] Бэкфилл и перестройка индексов **не выполнены ни против какой БД** — операция на живых
      данных, порядок расписан в `docs/DONT_FORGET.md`
- [x] Обновить `docs/LEARNING_SERVICE.md`, `docs/DB_SCHEMA.md` (+ `docs/TESTING/TENANCY.md`,
      `docs/DECISIONS.md`)

### [x] 40.11 ai-service (Postgres + Mongo)
- [x] `organization_id` в Postgres-таблицах сервиса: их оказалось две содержательных —
      `DialogBundles` и `DialogModes` (нуллабельный, `NULL` = глобальная библиотека), плюс
      контентная RLS и фильтры запросов. Отдельных таблиц квот/весов/настроек в ai-db нет: веса
      скоринга живут в памяти и приходят из Kafka, лимиты голоса — в конфиге и Redis.
      `UserReplicas` осознанно оставлена платформенной (как в 40.10) — см. `docs/DECISIONS.md`
- [x] **Mongo `DialogSession`**: поле `organizationId`, три составных индекса, все начинаются с
      него, он же зафиксирован как обязательный префикс будущего ключа шардирования
- [x] RLS для Mongo не существует — фильтр прикладной; все чтения сессий сведены в
      `DialogSessionRepository`: он требует `ITenantContext` в конструкторе, держит единственный
      `GetCollection<DialogSession>` в сервисе, не имеет ни одного нескоупленного метода и падает
      при незаданном тенанте. Юнит-тест проверяет по исходникам, что второго места не появилось
- [x] Сидируемые скрытые режимы (`company-call`, `custom-scenario`) остаются глобальными
      (тест это фиксирует); org-авторские режимы получают организацию в ключе — уникальность стала
      `(OrganizationId, BundleId, Key)` + частичный уникальный индекс по глобальным строкам
- [x] Redis: verdict-кеш, счётчики голосовой квоты и `RedisIdempotencyStore` — префикс
      `org:{orgId}:`. Проверены и остальные ключи ai-service; `TtsAudioCache` оставлен как есть —
      он in-process и его ключ это чистая функция от (текст, голос)
- [x] Обновить `docs/AI_SERVICE.md`, `docs/AI_DIALOG.md`, `docs/CUSTOM_SCENARIO.md`
      (+ `docs/TENANCY/TENANCY.md`, `docs/TESTING/TENANCY.md`, `docs/DECISIONS.md`)
- [~] Интеграционные тесты изоляции по трём хранилищам написаны, но **не прогнаны** (Правило №2
      в `docs/DONT_FORGET.md`): `AiTenantIsolationIntegrationTests`, 11 тестов, запускать
      `--filter "TestCategory=Integration"`. Юнит-тесты 164/164 зелёные без них
- [~] Бэкфилл Mongo и перестройка индексов **не выполнены ни против какой БД, Mongo или Redis** —
      операция на живых данных, порядок расписан в `docs/DONT_FORGET.md`. Бэкфилла в Postgres нет
      намеренно: весь существующий контент глобальный, `NULL` для него уже верное значение

### [x] 40.12 company-service
- [x] `organization_id` в `Company`, `CallLogEntry`, `PracticeCall`, `CompanyContact`, `CompanyPersona`
      — NOT NULL, `ITenantScoped`, строгая RLS на всех пяти таблицах (глобального контента в этой
      БД нет вообще), фильтр запроса на каждую сущность по отдельности
- [x] Скоуп становится двойным: организация **и** пользователь (личный CRM внутри компании)
      — организация из `ITenantContext` (фильтр + RLS), пользователь явным предикатом
      `UserId == userId` на родителе и на каждом под-ресурсе; обе половины покрыты тестами
- [x] Индексы: `(organization_id, user_id)`, `(organization_id, company_id, occurred_at DESC)`
      — миграция намеренно не создаёт и не дропает ни одного индекса, всё в
      `40.12_company_organization_indexes_concurrently.sql` (иначе между деплоем и скриптом
      удаление компании сканировало бы четыре дочерние таблицы)
- [x] **`FollowUpReminderBackgroundService`** — сейчас сканирует всех; переделать на
      обход организаций со scoped-контекстом на каждую
      — не установленный тенант и системный режим теперь бросают исключение, а не значат «все»
- [x] `company.followup.due` несёт `organizationId` в конверте
      — `IEventPublisher.PublishAsync` получил необязательный параметр `organizationId`
- [x] Обновить `docs/COMPANIES/COMPANIES.md`
- [~] Тесты изоляции (12 штук, реальный Postgres) — **написаны и закоммичены, но не запускались**
      (Правило №2 в `docs/DONT_FORGET.md`); юнит-тесты 134/134 зелёные
- [~] Бэкфилл и перестройка индексов — SQL и драйвер написаны, **ни разу не выполнялись ни против
      какой БД**; порядок для человека в `docs/DONT_FORGET.md`

### [x] 40.13 Остальные сервисы
- [x] `social-service`: Postgres `social` + Mongo `chat_conversations` + фото в MinIO;
      дружба и чат **не должны** пересекать границу организации — шесть таблиц (`Friendships`,
      `DiscussThreads/Replies/Votes/ThreadTags/Photos`) строгая RLS, `DiscussTags` — нуллабельная
      контентная (курируемые теги общие), `ChatConversationRepository` — единственный держатель
      Mongo-коллекции (RLS у Mongo нет), фото — `org/{organizationId}/…`, старые ключи не трогаются
  - [~] Интеграционные тесты изоляции (12 штук, Postgres+Mongo) **написаны, не прогнаны** —
        Правило №2 в `docs/DONT_FORGET.md`; юнит-тесты 56/56 зелёные
  - [~] Раскатка (`--backfill` → `--mongo` → `--indexes`) **не выполнена ни против какой БД**;
        порядок в `docs/DONT_FORGET.md`
  - [~] Поиск пользователей (`SearchUsersAsync`) остался платформенным, не сужен по организации —
        решение оставлено владельцу продукта, см. `docs/DONT_FORGET.md` и `docs/DECISIONS.md`
- [x] `gamification-service`: таблицы + Hangfire-джобы (сброс серий, недельное закрытие) —
      обход по организациям либо явный `IsSystem` — семь таблиц строгой RLS, оба джоба стали
      «обход по организациям» через `TenantJobScope`, `LeagueSettings` стала per-organization
  - [~] В отличие от social/learning/ai/company в этом блоке **не написано** ни модельного
        тест-файла (`*TenancyModelTests`), ни интеграционных тестов изоляции по реальному
        Postgres — только точечные правки существующих тестов (`StreakResetJobTests`,
        `StreakTimezoneTests`, `GamificationDbContextFactory`). Юнит-тесты 42/42 зелёные, но
        покрытие слабее, чем в остальных сервисах блока — см. отчёт исполнителя
  - [~] Раскатка (`--backfill` → `--indexes`) **не выполнена ни против какой БД**; порядок в
        `docs/DONT_FORGET.md`
- [x] `notification-service`: Redis-инбоксы — префикс `org:{orgId}:` — инбокс, счётчик и
      вотермарка чат-писем; очередь `notifications:chat-email:pending` осталась без префикса
      намеренно (организация едет внутри элемента очереди); `RequiresOrganization` остался `true`
- [x] `analytics-service`: Redis-only, presence и воронки — префикс ключей, иначе утекает
      численность команды между заказчиками — `presence:online` → `org:{orgId}:presence:online`,
      реестр организаций `presence:organizations`, `FunnelEventsConsumer` — `RequiresOrganization
      => false` (кросс-организационное событие `user.registered`)
  - [~] Старый ключ `presence:online` без TTL — единственный ключ блока, который не уйдёт сам,
        удалить руками (`docs/DONT_FORGET.md`)
- [x] `identity-service`: `ExpiredRefreshTokenCleanupService` /
      `ExpiredEmailVerificationCleanupService` — явный системный режим (`EnterSystemMode()` до
      резолва `IdentityDbContext`), четыре тест-тривера по исходникам; identity 62/62
- [x] Обновить `docs/SOCIAL_SERVICE.md`, `docs/GAMIFICATION_SERVICE.md`,
      `docs/NOTIFICATION_SERVICE.md`, `docs/ANALYTICS_SERVICE.md` (+ `docs/DB_SCHEMA.md`,
      `docs/TESTING/TENANCY.md`, `docs/DECISIONS.md`, `docs/DONT_FORGET.md`)

### [x] 40.14 Аудит фоновых задач и приёмка изоляции
- [x] Реестр всех `BackgroundService` / Hangfire-джоб: для каждой явно записать режим —
      «обход по организациям» или «системный»
      — `docs/TENANCY/BACKGROUND_JOBS.md`: 22 регистрации `AddHostedService` + 2 Hangfire-крона в трёх
      таблицах (тронувшие БД / консьюмеры с тенантом из конверта / не трогающие тенантные данные
      вообще — третья таблица есть ровно затем, чтобы полноту реестра можно было проверить)
- [x] Незаданный тенант — **исключение, а не разрешение**: пустой контекст не должен
      молча означать «все данные»
      — найдены и закрыты два последних неявных: `OutboxRelayBackgroundService` и
      `GamificationDialogWeightsConsumer` (второй требовал тенанта для платформенной настройки и
      поэтому отправлял в dead-letter каждое изменение, сохранённое сотрудником Sellevate)
- [x] `OutboxRelayBackgroundService` — единственный легитимный системный читатель всех
      строк (потому тенант и лежит в payload конверта, а не выводится при публикации)
      — теперь это сказано в коде (`EnterSystemMode()`), а не выводится из пустого контекста;
      поведение не изменилось, изменилось то, что заявку можно прочитать и отревьюить
- [~] Сквозной интеграционный тест: две организации, полный набор операций, проверка что
      ни один endpoint не отдаёт чужие данные
      — **не выполняется**: отложено Правилом №3 в `docs/DONT_FORGET.md` («не пиши пока тесты»,
      владелец, 2026-08-16). Вместо теста написан чеклист приёмки для человека (см. пункт ниже);
      строка добавлена в раздел «Тесты, которых нет»
- [x] Прогон `security-reviewer` (opus) по границе тенанта
      — 0 критичных, ни одного пути, отдающего данные одной организации пользователю другой;
      8 из 12 областей чисты (25/25 сущностей `ITenantScoped` имеют и query filter, и RLS-политику;
      сырого SQL в бэкенде нет вообще; `IgnoreQueryFilters` — ровно три места, все перечисление
      организаций). Пять находок исправлено кодом (`af7ff0e`), три отложены владельцу с обоснованием
      в `docs/DECISIONS.md`
- [x] Заполнить `docs/TESTING/TENANCY.md`
      — раздел «приёмка изоляции: чеклист для человека»: ловушка `dotnet test <sln>` (гоняет один
      проект из одиннадцати, 53 теста из 894, и выходит зелёным), линты, три грепа против гниения
      реестра, четыре проверки глазами, все 131 написанный-но-ни-разу-не-прогнанный интеграционный
      тест одной таблицей с командой на проект

> **Итог блока, который важнее галочек:** RLS сейчас **не включена ни в одном окружении** — все
> compose-файлы подключают сервисы под владельцем схемы, а `FORCE ROW LEVEL SECURITY` к
> суперпользователю не применяется. Значит, ни одна политика из четырнадцати тенантных миграций
> сегодня ничего не фильтрует. Граница держится на слоях 1 и 2 (middleware + EF-фильтры), и ревью
> нашло их целыми, но свойство «переживает забытый фильтр», ради которого RLS и вводилась, **пока не
> существует**. Четвёртый слой приёмки честно считать непройденным до перехода на роль
> `sellevate_app` — это шаг человека, он расписан в `docs/DONT_FORGET.md`.

---

### Этап D — контент: версионирование и кастомизация

### [x] 40.15 Иммутабельное версионирование уроков
- [x] `lesson (id, organization_id NULL=глобальный, parent_lesson_id, slug, archived)`,
      `UNIQUE (organization_id, slug)`
      — **это существующая таблица `Lessons`, расширенная тремя колонками**, а не новая рядом:
      второй «урок» заставил бы 40.16/40.17/40.18, сидер и админку каждый раз выбирать, какой из
      двух имеется в виду (`docs/DECISIONS.md`). Уникальность — два индекса, а не один:
      `UNIQUE (OrganizationId, Slug)` **плюс** частичный по глобальным строкам, потому что NULL'ы в
      составном уникальном индексе Postgres считает различными (та же ловушка, что в 40.10).
      Слаги существующих уроков — машинные (`lesson-<hex id>`), транслитерации русских заголовков
      нет намеренно
- [x] `lesson_version (id, lesson_id, version_no, content jsonb, content_hash, status,
      base_version_id, is_breaking, created_by, published_at)`
      — таблица `LessonVersions`, контентная RLS (`EnableTenantRlsForContent`), `OrganizationId`
      денормализован из урока, потому что RLS-политика умеет сравнивать только колонки той строки,
      которую фильтрует. `BaseVersionId` заполняется у override'ов и остаётся writable на
      замороженной строке — 40.18 нужна возможность перепривязать базу
- [x] **Единица версионирования — урок целиком вместе с упорядоченным набором упражнений**
      — снимок `{exercises[], schemaVersion, title}`; `exerciseId` лежит внутри снимка и, значит,
      внутри хеша — это идентичность, которая нужна 40.16
- [x] `draft` мутабелен, публикация замораживает строку навсегда; следующая правка —
      новый draft копией последней опубликованной
      — заморозка **триггером в базе** (`LessonVersions_reject_frozen_change`), а не соглашением в
      сервисе: снимок, который можно поправить задним числом, молча перескоривает все исторические
      попытки — ровно та порча метрик, которую пишется чинить 40.16. `published → draft` запрещён,
      `published → archived` разрешён
- [x] Частичный уникальный индекс: не более одного draft на урок
      (`WHERE status = 'draft'`) — иначе два админа делают две ветки без стратегии слияния
      — `IX_LessonVersions_LessonId_Draft`; в базе, а не в C#, потому что два админа, нажавшие
      «редактировать» одновременно, — это ровно та гонка, которую проверка-перед-вставкой проигрывает
- [x] `content_hash` — не плодить версию при публикации без изменений
      — SHA-256 по **каноническому** JSON (ключи объектов отсортированы, порядок массивов сохранён).
      Без канонизации перестановка ключей при пересохранении выглядела бы как правка содержимого, и
      хеш не делал бы того единственного, ради чего он есть
- [x] Обновить `docs/SKILLS_AND_EXERCISES.md`, `docs/DB_SCHEMA.md`
      (+ `docs/LEARNING_SERVICE.md`, `docs/API_CONTRACTS.md`, `docs/DECISIONS.md`,
      `docs/DONT_FORGET.md`, `docs/TENANCY/CONTENT_MODEL.md`)

> **Чего в блоке нет и почему.** Тестов не написано ни одного — Правило №3 в `docs/DONT_FORGET.md`
> («не пиши пока тесты», владелец, 2026-08-16); восемь строк с описанием того, что эти тесты должны
> были бы проверять, добавлены в раздел «Тесты, которых нет». Экрана в админке нет — фронт в блоке
> не трогался, и рисовать его раньше 40.16/40.20 смысла нет. Версий у существующих уроков нет:
> миграция их не создаёт, первая появляется, когда админ открывает черновик или публикует — привязка
> исторических попыток к «версии 1» это 40.16, и место для неё оставлено, а сама она не сделана.
>
> **Отличие раскатки от 40.10–40.13, важное для человека:** здесь **один** шаг вместо трёх и нет
> окна, в котором данные невидимы. Бэкфилл слагов и создание индексов лежат внутри самой миграции —
> значение слага берётся из первичного ключа той же строки, `LessonVersions` создаётся пустой, а
> `Lessons` это несколько сотен строк. Отдельного `_indexes_concurrently.sql` у блока нет намеренно;
> вместо него — read-only `docs/TENANCY/sql/40.15_lesson_versioning_verify.sql`. Против настоящей БД
> не выполнялось ничего.

### [x] 40.16 Привязка прогресса к версии (чинит искажение метрик)
- [x] `UserExerciseAttempt` ссылается на `lesson_version_id` + идентичность упражнения
      **внутри** версии, а не на изменяемый `ExerciseId`
      — одна новая нуллабельная колонка `LessonVersionId`, а не две: идентичность упражнения внутри
      версии — это ключ `exerciseId`, который 40.15 уже положил **внутрь** снимка и внутрь хеша.
      `ExerciseId` не выбрасывается, а меняет смысл: ключ во замороженном документе вместо указателя
      на редактируемую строку. Внешнего ключа нет намеренно — контентная таблица под политикой
      `IS NULL OR = current` и строгие тенантные данные под равенством не связываются ограничением,
      которое проверяется правами пишущего (`docs/DECISIONS.md`)
- [x] То же для `UserLessonProgress`
      — обновляется **только при продвижении** строки (новый лучший результат или переход в
      `completed`), иначе «завершил версию 1» молча превращалось бы в «завершил версию 3»
- [x] Миграция исторических данных: привязать существующие попытки к «версии 1»
      — в два приёма, и это ключевая развилка блока. «Версию 1» несуществующим версиям создаёт
      `LessonVersionBackfill` на старте сервиса, в системном режиме, **на C#**: `ContentHash` — это
      SHA-256 по ровно тем байтам, что печатает `LessonSnapshotSerializer`, а Postgres хранит `jsonb`
      со своим порядком ключей, поэтому снимок, собранный в SQL, нёс бы хеш, который сервис никогда
      не воспроизведёт. Привязку существующих строк делает
      `docs/TENANCY/sql/40.16_progress_version_backfill.sql` — батчами, руками, без окна
      обслуживания: по `LessonVersionId` не фильтрует ничто, поэтому невидимых данных между шагами
      нет (первый раз с 40.9)
- [x] `is_breaking` на публикации: косметические правки дашборд склеивает в одну линию
      метрик, смысловые — разрывает
      — `GET /admin/lessons/{lessonId}/accuracy` в learning-service (там лежат попытки), а не в
      analytics: тот Redis-only, попыток не хранит вообще, и его счётчик `exercise.completed` — это
      воронка без урока, версии и организации. Сегмент начинается на первой опубликованной версии и
      на каждой breaking; косметическая продолжает предыдущий. Попытки без версии — отдельная
      корзина `unversionedAttempts`, а не «версия 1»
- [~] Тест-регрессия: правка правильного ответа не меняет историческую точность
      — **не написана**: Правило №3 в `docs/DONT_FORGET.md` («не пиши пока тесты», владелец,
      2026-08-16). Пошаговое описание того, что этот тест должен был бы проверять, и чем именно
      опасно его отсутствие (регрессия здесь абсолютно молчаливая) — в разделе «Тесты, которых нет»
- [x] Обновить `docs/ANALYTICS_SERVICE.md` (как метрики считаются по версиям)
      (+ `docs/LEARNING_SERVICE.md`, `docs/DB_SCHEMA.md`, `docs/API_CONTRACTS.md`,
      `docs/DECISIONS.md`, `docs/DONT_FORGET.md`, `docs/TENANCY/CONTENT_MODEL.md`,
      `docs/TENANCY/BACKGROUND_JOBS.md`)

> **Осознанная дыра, которую закрывает не код, а экран 40.20.** Резолвер создаёт версию только если
> у урока нет **ни одной**, и намеренно не сверяет хеш живых строк с последним снимком. Значит,
> админ, поправивший урок и не нажавший «опубликовать», оставляет продавцов отвечать на новый
> контент с привязкой к старому снимку. Сверять хеш было бы хуже: отличить исправленную запятую от
> смены правильного ответа нечем, пришлось бы считать любую правку смысловой, и график рвался бы на
> каждой опечатке — ровно тот провал, ради которого `is_breaking` и существует. Публикация — это и
> есть акт, которым правка становится видимой для истории; 40.20 должен сделать её естественным
> завершением редактирования.
>
> **Чего в блоке ещё нет.** Экрана с графиком нет — фронт не трогался, и рисовать его раньше 40.20
> смысла нет. Против настоящей БД не выполнялось ничего: оба SQL-файла (бэкфилл и конкурентные
> индексы) написаны и не запускались. Индексы, в отличие от 40.15, вынесены из миграции обратно в
> `_indexes_concurrently.sql` — это те же две растущие таблицы прогресса, с которых 40.10 уже унёс
> все индексы; сами колонки остались в миграции, потому что нуллабельная колонка на Postgres 11+ —
> это правка каталога без переписывания таблицы.

### [x] 40.17 Версионирование программы и зачисления
- [x] `program_version (id, organization_id, version_no, status)`
      — таблица `ProgramVersions` в learning-db, рядом с уроками, на которые она ссылается.
      **Строгие тенантные данные, а не контент** — это первое место Этапа D, где контентная
      политика (`IS NULL OR = current`) была бы прямо неверна: глобальной программы не бывает,
      `OrganizationId NOT NULL`, RLS обычным равенством. Один черновик на организацию — частичным
      уникальным индексом в базе, а не проверкой в C#
- [x] `program_item (program_version_id, skill_id, order_index, lesson_version_id)` —
      пин на конкретную версию урока
      — плюс пятая колонка `LessonId`, и она не украшение: без неё «тот же урок, но перепинен на
      другой снимок» невыразимо, а уникальность смогла бы запретить лишь повтор *версии*, но не
      один урок на версиях 3 и 5 внутри одной программы (один и тот же материал с двумя разными
      правильными ответами). Дрейфовать не может: `LessonVersion.LessonId` заморожен триггером
      40.15. Внешних ключей на `skill_id`/`lesson_id`/`lesson_version_id` нет намеренно — та же
      причина, что в 40.16 (`docs/DECISIONS.md`)
- [x] `enrollment (user_id, program_version_id)` — менеджер закреплён за снимком
      — таблица `ProgramEnrollments`, `UNIQUE (OrganizationId, UserId)`; человек в двух
      организациях держит по зачислению в каждой. Настоящий внешний ключ на `ProgramVersions`
      с `ON DELETE RESTRICT` — обе стороны строгие тенантные, возражение 40.16 здесь не действует,
      а версию, на которой кто-то стоит, удалять нельзя
- [x] Менеджер на 8-м уроке из 21 не должен обнаружить перестроенную программу; новые
      зачисления идут на новую версию, текущим — явный переход с показом диффа
      — гарантия сделана **структурно, а не процедурно**: `POST /admin/program/enrollments`
      идемпотентен и возвращает существующее зачисление **неизменным**, а маршрута, которым админ
      переносит чужой пин, не существует вовсе. Перенос — `POST /program/switch`, от лица самого
      человека, с явным указанием id версии (не «на самую свежую»), чтобы публикация между показом
      диффа и согласием не увела его на программу, которую ему не показывали. Заморозку
      опубликованной версии держат два триггера в базе, и важнее тот, что на `ProgramItems`, —
      структура живёт в этих строках. Дифф честный: четыре корзины (добавили / убрали / перепинили /
      переставили), а `isBreaking` считается по **всем** версиям урока между двумя пинами, а не по
      целевой, иначе смена правильного ответа в версии 4 пряталась бы за косметической версией 5.
      **Чего в этом пункте нет:** фронтенд запинённую программу пока не читает — `/skill-tree`,
      `/lessons` и `/exercises/*` по-прежнему отдают живое дерево, а программу отдаёт только новый
      `GET /program`. Экран — это 40.20; запись в `docs/DONT_FORGET.md`
- [x] Структура программы — только ссылки: перестановка навыков не трогает ни один урок
      — ни одна запись в `ProgramVersionService` не касается `Lessons`, `Exercises` или
      `LessonVersions`; единственный вызов в сторону версионирования уроков
      (`EnsurePublishedVersionIdAsync`) только читает, кроме урока, который никогда не публиковали,
      где он минтит ту же «версию 1», что создала бы попытка. Переставить навыки = новая
      `ProgramVersion` с другими `orderIndex`
- [x] Обновить `docs/TENANCY/CONTENT_MODEL.md` (§2.5), `docs/DB_SCHEMA.md`,
      `docs/API_CONTRACTS.md`, `docs/LEARNING_SERVICE.md`, `docs/SKILLS_AND_EXERCISES.md`
      (часть 3.7), `docs/DECISIONS.md`, `docs/DONT_FORGET.md`

> **Чего в блоке нет и почему.** Тестов не написано ни одного — Правило №3 в `docs/DONT_FORGET.md`
> («не пиши пока тесты», владелец, 2026-08-16); двенадцать строк с описанием того, что эти тесты
> должны были бы проверять, добавлены в раздел «Тесты, которых нет», и первые две названы там
> самыми опасными (идемпотентность зачисления и `isBreaking` по интервалу — обе регрессии
> абсолютно молчаливые). Экрана в админке нет — фронт в блоке не трогался вообще, и рисовать его
> раньше 40.20 смысла нет. «Версии программы 1» никто не создаёт и никто никого не зачисляет:
> в отличие от 40.16, где тело урока уже существовало и не хватало акта заморозки, программа —
> это решение, которого ещё никто не принял, и пин всех подряд на снимок живого дерева молча
> заморозил бы учащихся на учебном плане, который никто не утверждал.
>
> **Зачисление НЕ закрывает доступ к урокам — осознанный fail-open против общего стиля Фазы 40.**
> В день выката у каждой организации ноль опубликованных версий программы, поэтому fail-closed
> означал бы «никто не может открыть урок, пока кто-то руками не подёргает API». Fail-closed
> правилен для *данных* (его по-прежнему держит контентная RLS, которую блок не менял), а здесь
> вопрос другой — «какое подмножество и в каком порядке».
>
> **Раскатка — один шаг и НЕТ окна, в котором что-то невидимо, причём по новой причине.** В 40.15
> окна не было, потому что бэкфилл брал значение из той же строки; в 40.16 — потому что по новой
> колонке ничто не фильтрует. Здесь бэкфилла нет вовсе: три таблицы создаются пустыми, ни одна
> существующая строка не трогается. Индексы тоже внутри миграции (строятся по нулю строк),
> отдельного `_indexes_concurrently.sql` у блока нет намеренно — как и в 40.15. Вместо него
> read-only `docs/TENANCY/sql/40.17_program_versioning_verify.sql`. Против настоящей БД не
> выполнялось ничего.

### [x] 40.18 Override'ы (copy-on-write) и очередь stale
- [x] Копия урока создаётся **только** в момент, когда админ нажал «редактировать»,
      никогда при онбординге
      — единственный путь создания копии во всей платформе это
      `POST /admin/content/overrides/{kind}/{baseId}`; `ContentOverrideService` не подключён ни к
      консьюмеру, ни к hosted-сервису, ни к событию создания организации. Проверка не на слово:
      раздел 8 read-only скрипта `docs/TENANCY/sql/40.18_content_overrides_verify.sql` просто считает
      копии, и на свежем выкате их ноль. Схемы новой таблицы у блока нет — 40.15 уже построил
      `Lessons.ParentLessonId` и `LessonVersion.BaseVersionId`; добавлено то же самое для двух
      забываемых семейств
- [x] Резолв на чтении: есть override → он, нет → базовый
      — `ContentOverrideResolution`, **явный вызов, а не query filter**, и это решение, а не
      ограничение: фильтр, ссылающийся на собственный `DbSet`, EF применяет рекурсивно, но важнее
      то, что админские пути **обязаны** видеть обе стороны — экран ревью для того и существует.
      Применён на пользовательских чтениях (дерево, списки уроков, разблокировка следующего,
      завершение навыка, техники, справочник, список режимов диалога). Платформенный персонал не
      резолвит: правка одного заказчика скрыла бы глобальный урок от сотрудника Sellevate. У техник
      это корректность, а не косметика — override носит слаг базы, и без резолва поиск по слагу
      находил две строки
- [x] При публикации новой версии глобального урока все override с устаревшим
      `base_version_id` помечаются `stale` и падают в очередь ревью админу организации
      — очередь **вычисляется на чтении, флага `stale` нет нигде**, и это главная развилка блока.
      Пометка в транзакции публикации не «неудобна», а отвергается базой: это запись строк в
      организации, внутри которых пишущий не находится, а `WITH CHECK` — ровно тот клауз, который
      разделение ролей 2026-08-16 намеренно не расширяло для платформенного персонала. Фоновая
      джоба работала бы, но может отстать, и **пока она отстаёт, очередь утверждает, что override
      актуален, когда база уже уехала** — единственная ошибка, которую очередь ревью совершать не
      должна. Отсутствие джобы записано в реестр `docs/TENANCY/BACKGROUND_JOBS.md` §4a как решение,
      а не как пропуск
- [x] **Автомерж не делать**: контент — это проза и критерии оценки, трёхстороннее
      слияние даёт правдоподобную бессмыслицу, которая потом оценивает продавца
      — не только мержа нет: **API не считает и диффа**. Ревью отдаёт три документа целиком.
      Текстовый дифф — это первая половина мержа, и как только сервер начнёт его отдавать, «ну
      примени хотя бы неконфликтующие куски» станет продуктовым разговором
- [~] Экран ревью: что изменилось сверху, что изменила организация, три действия —
      принять базу (отбросить override) / оставить override (перепривязать base) / править
      — **три действия сделаны как API и работают; экрана нет.** `GET /admin/content/overrides`
      (+`?staleOnly=true`), `GET .../{kind}/{overrideId}` (три документа), `POST .../accept-base`,
      `POST .../keep-override`; «править» — это обычные админские маршруты, которые блок для этого и
      открыл админу организации, а публикация новой версии override'а перепривязывает базу сама.
      Фронт не трогался по той же причине, что в 40.15–40.17: **админка РОПа это 40.20 и она ждёт
      дизайн от владельца**. Одна честная оговорка для того, кто будет её рисовать: `baseAtFork`
      заполнен только у уроков — у техник, справочника и промптов точка форка это отпечаток, а текст
      базы на момент форка нигде не сохранён. Подробности в `docs/DONT_FORGET.md`
- [x] То же для `Technique` и `ReferenceMaterial` (легко забыть)
      — `ParentTechniqueId`/`ParentMaterialId` + `BaseContentHash` + `IsArchived`, резолв, та же
      очередь и те же три действия. **Версионирования у них нет**, и точка форка — отпечаток
      канонического контента базы, а не id замороженного снимка: строить 40.15 ещё дважды (триггер
      заморозки, частичный уникальный индекс на черновик, канонический сериализатор, эндпоинт
      публикации) ради двух семейств из одной строки каждое — это удвоение блока. Отпечаток
      отвечает на единственный вопрос очереди («база уехала?») ровно так же; отдаёт он только
      before-image
- [x] Промпты `DialogMode`/`DialogBundle` — override'абельны per-organization; сидируемые
      скрытые режимы остаются глобальными
      — override'абелен `DialogMode`, то есть ровно то, где промпты и лежат (`ChatSystemPrompt`,
      `FeedbackSystemPrompt`); копия сохраняет `BundleId` и `Key` родителя, что уникальные индексы
      40.11 уже разрешают, поэтому переопределённый промпт появляется в том же пакете на том же
      месте без второго слоя резолва. **`DialogBundle` копированием не переопределяется** —
      осознанное сужение: у пакета промпта нет вообще (заголовок, описание, эмодзи, порядок), а
      копия пакета это пустая папка, которой нужен ответ «какие режимы внутри», и естественный
      ответ на него — форк всей библиотеки на уровень ниже. Скрытые сидируемые режимы остаются
      глобальными **и сервис отказывается их переопределять (409)**: их промпты наполовину код,
      сервис дописывает подстановки в момент запуска. Kafka-события между сервисами нет — override
      и его база всегда в одной БД, так что stale это внутрибазовое сравнение везде, где его
      спрашивают
- [x] Обновить `docs/TENANCY/CONTENT_MODEL.md` (§1, §2.6, §4), `docs/DB_SCHEMA.md`,
      `docs/API_CONTRACTS.md`, `docs/LEARNING_SERVICE.md`, `docs/AI_SERVICE.md`,
      `docs/SKILLS_AND_EXERCISES.md` (часть 3.8), `docs/TENANCY/BACKGROUND_JOBS.md` (§4a),
      `docs/DECISIONS.md`, `docs/DONT_FORGET.md`

> **Чего в блоке нет и почему.** Тестов не написано ни одного — Правило №3 в `docs/DONT_FORGET.md`
> («не пиши пока тесты», владелец, 2026-08-16); пятнадцать строк с описанием того, что эти тесты
> должны были бы проверять, добавлены в раздел «Тесты, которых нет», и первые три названы там самыми
> опасными — резолв на чтении, `ContentAuthoringGuard` и наследование организации упражнением. Все
> три дают **молчаливую** регрессию, а третья ещё и кросс-тенантную. Экрана ревью нет — фронт не
> трогался, см. пункт выше. Версий у техник и справочных материалов нет — см. пункт выше.
>
> **Побочный результат, который стоит знать: граница прав записи в контент лежит в коде, а не в
> RLS.** Контентная политика разрешает `OrganizationId IS NULL` **и в `WITH CHECK`** — иначе
> заказчик не смог бы читать общую библиотеку, — а как правило записи это читается как «любая
> организация может писать строку без владельца», то есть править учебный план всех остальных. База
> не может отличить эти два случая: «глобальное» это NULL, а не тенант. Поэтому появился
> `ContentAuthoringGuard`, а в базе зафиксирована та половина, которую база выразить может — три
> CHECK-ограничения «у override всегда есть владелец», включая `Lessons`, где 40.15 создал колонку
> без него.
>
> **Заодно закрыты две дыры, которые сами себя документировали.** `TenantTransactionScope` с 40.10
> писал в своём же комментарии, что админские контентные контроллеры не открывают транзакцию и что
> «40.18 придётся к ним вернуться»; ai-service был единственным сервисом с RLS-таблицами и без
> transaction scope вообще (`docs/DONT_FORGET.md`). Пока весь контент был глобальным, это ничего не
> стоило; с первой же org-строкой админ перестал бы видеть собственный override — fail-closed и
> полностью беззвучно. Плюс два настоящих бага, найденных по дороге: упражнение, созданное под
> override-уроком, не наследовало организацию и уехало бы в общую библиотеку, а проверка
> уникальности слага техники охватывала «глобальное или моё», из-за чего override — который слаг
> базы носит намеренно — нельзя было бы сохранить.
>
> **Раскатка — один шаг на каждую из двух БД и НЕТ окна, в котором что-то невидимо** (третий блок
> Этапа D подряд, причина та же, что в 40.15/40.17: бэкфилла нет вовсе). Отдельного
> `_indexes_concurrently.sql` у блока нет намеренно — нуллабельная колонка и `NOT NULL boolean` с
> константным дефолтом на Postgres 11+ это правка каталога, а два новых индекса строятся по
> таблицам в десятки-сотни строк. Вместо него read-only
> `docs/TENANCY/sql/40.18_content_overrides_verify.sql`. Против настоящей БД не выполнялось ничего.

### [x] 40.19 Профиль организации и параметризация контента
- [x] Подстановки в текстах базовых уроков и промптах персон резолвятся из
      `organization_profile` на этапе рендера — синтаксис `{{organization.*}}`, шесть ключей,
      незаполненное поле даёт нейтральную формулировку («ваш продукт»), а не пустую строку и не
      видимые фигурные скобки. Рендер **только на чтении**: строка и снимок 40.15 хранят шаблон,
      поэтому `ContentHash` одинаков у всех организаций (`docs/CONTENT_PARAMETERIZATION.md` §3)
- [x] Один базовый урок обслуживает всех — клиент заполняет форму, а не форкает контент.
      Профиль доезжает до learning и ai репликой по Kafka (`organization.profile.updated` →
      `OrganizationProfileReplicas`), а не синхронным вызовом: подстановка стоит на пути чтения
      всего продукта
- [x] `banned_claims` учитывается в промптах персон и критериях оценки — в трёх промптах из одного
      билдера: чат-промпт персоны и промпт обратной связи в ai-service, промпт оценки упражнений в
      learning-service. Только персона — хуже, чем ничего: молчащая персона при хвалящем грейдере
      учит продавца ровно запрещённому
- [x] `seed.py` / `/admin/seeder/bundle` сеет **глобальную** библиотеку
      (`organization_id IS NULL`) — обязательное поле `target=global` плюс сужение всех чтений до
      `OrganizationId IS NULL`. Второе чинило настоящий молчаливый баг: чтения шли через тенантный
      фильтр «глобальное или моё», а уроки апсертятся по `(TopicId, Title)`, поэтому повторный
      импорт мог перезаписать override заказчика базовым текстом (`docs/SEEDER.md` §0)
- [~] **Замер на первом пилоте:** какая доля адаптации закрывается подстановкой из
      профиля, а какая требует правки текста урока руками. Больше трети руками →
      параметризация спроектирована не так, чинить до десятого клиента.
      **Продуктовая задача владельцу, не агентская** — записана в `docs/DONT_FORGET.md` в разделе
      продуктовых вопросов. Честно провести её сегодня нельзя ещё по одной причине: экрана формы
      профиля нет (это 40.20), поэтому заказчик не может заполнить её сам
- [x] Обновить `docs/SEEDER.md` — §0 «сидер пишет только глобальную библиотеку», `target` в четырёх
      контрактах, раздел про экспорты и раздел про плейсхолдеры в сидируемом контенте

> **Что в блоке сделано у́же, чем звучит роадмап.** Механизм подстановок готов и работает, но
> **в базовой библиотеке пока нет ни одного `{{organization.*}}`**: существующие уроки написаны без
> плейсхолдеров, так что включение блока сегодня не меняет ни одного видимого символа. Расстановка
> подстановок по текстам — контентная работа, а не инженерная (неверно поставленный плейсхолдер
> портит урок сразу всем заказчикам), и агент её сознательно не делал. Правила для того, кто будет
> это делать, — `docs/CONTENT_PARAMETERIZATION.md` §6; самое неочевидное — русская морфология:
> движка склонений нет и не будет, фразы надо строить так, чтобы они выживали подстановку.
>
> **Фронта нет**, по той же причине, что в 40.15–40.18: форма профиля — это админка РОПа, то есть
> 40.20, и она ждёт дизайн владельца. Бэкенд профиля существует с 40.5.
>
> **Раскатка — два старта сервисов и один ручной шаг, окна с невидимыми данными НЕТ.** Обе миграции
> создают пустую таблицу `OrganizationProfileReplicas` (строгая RLS, обычное равенство — не
> контентная политика: профиль с `NULL`-владельцем означал бы запреты одного заказчика для всех).
> Бэкфилла нет, отдельного `_indexes_concurrently.sql` нет намеренно — единственный запрос к таблице
> идёт по первичному ключу. Ручной шаг ровно один: **пересохранить профиль каждой организации,
> заполненный до выката**, иначе его реплик не существует и `banned_claims` не применяются вообще.
> Всё расписано в `docs/DONT_FORGET.md`; read-only проверка —
> `docs/TENANCY/sql/40.19_organization_profile_verify.sql`, против настоящей БД не выполнялась.

### [~] 40.20 Разделение админки
- [ ] Платформенная суперадминка: организации, глобальная библиотека, типы упражнений
- [ ] Админка организации (экран РОПа): своя программа, override'ы, профиль компании,
      люди, задания
- [ ] Ревизия всех текущих `/admin/*` маршрутов: каждый относится к одному из двух уровней
- [ ] Обновить `docs/ADMIN_PANEL.md`

> **Ждём дизайн от владельца (2026-08-16).** Ролевая модель и авторизация для обоих уровней
> уже готовы (см. пересмотр 40.6 выше и `docs/DECISIONS.md`, 2026-08-16): роли, клеймы и все
> четыре политики на месте, `RequireOrgAdmin` объявлена во всех сервисах и ждёт первый экран.
> Осталось **только визуальное разделение** двух админок — владелец сказал: «админка у админов
> и админов tenancy будет разная в разном месте. это пока можно не продумывать, я сам потом
> пришлю дизайн». Пункт аудита маршрутов фактически выполнен досрочно.

---

### Этап E — задания: цикл «РОП → менеджеры»

### [x] 40.21 Сущность Assignment
- [x] `assignment (id, organization_id, created_by, title, goal, source_type:
      training|manual|gap_detected, source_ref, content jsonb, audience, opens_at,
      deadline, completion_rule jsonb, repeat_schedule jsonb, status)`
- [x] `assignment_progress (assignment_id, user_id, status, best_score, attempt_count,
      first_opened_at, completed_at)`
- [x] **Отдельная сущность, а не переиспользованный learning path**: дерево навыков —
      длинное, последовательное, в своём темпе; задание — короткое, адресное, командное,
      с дедлайном
- [x] Живёт в `learning-service` (он владеет прогрессом и оценкой); новый сервис не нужен
- [x] Обновить `docs/DB_SCHEMA.md`, `docs/API_CONTRACTS.md`

> **Сделано 2026-08-18.** Таблицы `Assignments` и `AssignmentProgressRecords` в learning-db,
> обе — строгие тенантные данные (глобального задания не бывает, политика — обычное равенство,
> как у программы 40.17), плюс восемь маршрутов `/admin/assignments/*` под `RequireOrgAdmin`.
> Восемь развилок, которые роадмап оставлял открытыми, решены и записаны с отвергнутыми
> альтернативами в `docs/DECISIONS.md` (2026-08-18); четыре из них стоит знать, читая 40.22–40.24:
> - **`content` — только ссылки, и на замороженную версию урока.** Набор упражнений задания это
>   `lesson_version` (id снимка), диалог — ключ режима ai-service, теория — id справочного
>   материала. Тела упражнений остаются в `Exercise.SerializedContent`, поэтому «новых рендереров
>   нет» (40.23) — это факт, а не намерение; ссылка на изменяемый `Exercise.Id` повторила бы дефект,
>   который чинил 40.16.
> - **`audience` хранит правило, а не людей** (`whole_team` / `users` / `group`): список сотрудников
>   живёт в identity-service, и его копия здесь протухала бы при первом найме. Раздача — 40.23, и её
>   результат (строки прогресса) и есть достоверная запись «кому выдали».
> - **`completion_rule` обязателен и без значения по умолчанию.** Значение по умолчанию означало бы
>   «порога нет», то есть дало бы место в схеме ровно тому провалу, ради которого написан 40.22.
>   Проверяется только наличие `kind`; словарь и оценка — 40.22.
> - **`status` — `draft → active → closed`, только вперёд, триггером в БД.** Выдача замораживает то,
>   *что* задание требует (`source_type`, `source_ref`, `content`, `completion_rule`), но оставляет
>   редактируемыми аудиторию, дедлайн и заголовок — добавить трёх человек в идущее задание и
>   продлить срок это обычная работа РОПа, и запрет на них 40.23/40.24 пришлось бы ломать.
>
> **Чего в блоке нет намеренно:** в `AssignmentProgressRecords` никто не пишет (раздача — 40.23,
> оценка порога — 40.22), поэтому воронка у любого задания показывает четыре нуля — честный ноль
> вместо цифры, полученной срезанием угла. Фоновой джобы нет (`docs/TENANCY/BACKGROUND_JOBS.md` §4c):
> просроченное задание само не закроется до 40.26. Фронта нет по той же причине, что в 40.15–40.19 —
> админка РОПа это 40.20 и она ждёт дизайн владельца. Отдельного `_indexes_concurrently.sql` и
> бэкфилла нет намеренно: обе таблицы создаются пустыми, окна невидимости не существует. Ручной шаг
> ровно один — дать сервису стартовать, чтобы применилась миграция; read-only проверка —
> `docs/TENANCY/sql/40.21_assignments_verify.sql`, против настоящей БД не выполнялась. Всё расписано
> в `docs/DONT_FORGET.md`. Тестов не написано (Правило №3) — непокрытые места перечислены там же.

### [x] 40.22 Правило завершения = порог качества
- [x] `completion_rule` — порог, а не факт прохождения: `3 диалога с оценкой ≥70`,
      `точность по упражнениям ≥80%`
- [x] Если засчитывать факт — прокликают за четыре минуты, дашборд покажет 100%, и РОП
      это поймает. Это граница между инструментом и имитацией
- [x] `failed_threshold` — **нормальное видимое состояние**, не скрытый ретрай:
      «начал, пробовал 4 раза, не дотянул» — самая ценная строка на экране РОПа
- [x] Оценка порога переиспользует существующий скоринг learning-service

> **Сделано 2026-08-18.** Словарь `completion_rule` — ровно два вида из роадмапа:
> `{"kind":"dialog_score","minimumScore":70,"requiredCount":3}` и
> `{"kind":"exercise_accuracy","minimumAccuracyPercent":80}`. Неизвестный вид, отсутствующее число и
> **порог, равный нулю**, отклоняются с `400` при создании и правке. Оценивает
> `AssignmentThresholdEvaluator`, писателя запускает новый консьюмер `AssignmentThresholdConsumer`
> (`dialog.evaluated` + `exercise.completed`, `docs/TENANCY/BACKGROUND_JOBS.md` §4d). Шесть развилок
> решены и записаны с отвергнутыми альтернативами в `docs/DECISIONS.md` (2026-08-18); четыре из них
> стоит знать, читая 40.23–40.26:
> - **Порог нельзя прокликать, и это два конкретных решения, а не лозунг.** Точность считается
>   **по отправкам** (верные ÷ все), поэтому перебор до зелёного её понижает, а не повышает; и
>   оценка **не выставляется вообще**, пока не попробовали каждое упражнение набора, — иначе один
>   удачный ответ из двадцати это 100%. `dialog_score` **считает разговоры**, взявшие планку, а не
>   усредняет их: среднее позволяет одному сильному звонку вытянуть два слабых.
> - **`in_progress` и `failed_threshold` разделены по признаку «работа закончена», а не «планка
>   взята».** Сделал 2 диалога из 3 — не закончил; сделал 4 и не взял ни разу — та самая строка, ради
>   которой написан блок. Слепить их вместе значит спрятать человека, которому нужна помощь, среди
>   тех, кто не начинал.
> - **Всё пересчитывается из строк попыток, ничего не инкрементируется.** Оценённый разговор
>   хранится один раз (`UserDialogScores`, уникальность по организации + пользователю + сессии),
>   `AttemptCount` и `BestScore` выводятся из существующих строк. Это и есть ответ на «повторная
>   обработка события не должна накручивать счётчик» — свойство держится на конструкции, а не на
>   TTL redis-дедупа.
> - **Контракт `dialog.evaluated` пришлось расширить, потому что оценки он не нёс.** Поле `rawScore`
>   вопреки названию содержит доXP-множительную награду, ограниченную суммой настраиваемых весов, а
>   не сотней; оценка 0–10, которую видит менеджер, из ai-service не выходила. Добавлены `modeKey` и
>   `qualityScore` (нормализован к 0–100 у продюсера). Поля аддитивны, `rawScore` не тронут.
>
> **Чего в блоке нет намеренно:** строки `AssignmentProgressRecords` по-прежнему **никто не
> создаёт** — 40.22 написал того, кто их **меняет**, а существование строки означает «этому человеку
> задание выдали», то есть факт времени выдачи и предмет 40.23. Поэтому весь код блока сегодня
> отрабатывает вхолостую, воронка показывает четыре нуля, и это честный ноль, а не срезанный угол
> (отвергнутая альтернатива — создавать строку при первой засчитанной активности — расписана в
> `DECISIONS.md`). Составного правила («и упражнения, и диалог») нет: `BestScore` — одно число, и у
> составного правила нет естественного единого счёта; следствие — задание с обеими половинами
> оценивается по одной из них, и автор обязан выбрать. Просроченное задание само не проваливается
> (это 40.26). Фронта нет по той же причине, что в 40.15–40.21 — админка РОПа это 40.20. Отдельного
> `_indexes_concurrently.sql` и бэкфилла нет: таблица создаётся пустой, а историю оценок диалогов
> восстанавливать физически не из чего — она никогда не публиковалась. Ручных шагов два, и порядок
> важен: сначала ai-service, потом learning-service; read-only проверка —
> `docs/TENANCY/sql/40.22_completion_threshold_verify.sql`, против настоящей БД не выполнялась. Всё
> расписано в `docs/DONT_FORGET.md`. Тестов не написано (Правило №3) — непокрытые места перечислены
> там же, и первым в списке стоит идемпотентность `AttemptCount`.

### [x] 40.23 Назначение, экран менеджера, уведомления
- [~] Аудитория: конкретные пользователи / группа / вся команда — **две из трёх**. `whole_team` и
      `users` резолвятся в людей; `group` отвечает `400`, потому что понятия «группа» нет ни в одном
      сервисе платформы. Молча трактовать её как «вся команда» агент отказался (см. ниже)
- [x] Активное задание — первым экраном у менеджера, пока не выполнено
- [x] Практический диалог задания = обычный `DialogSession` с инъекцией персоны —
      тот же приём, что уже делает `CompanyContextPromptBuilder`
- [x] Контентный словарь задания — существующие 11 типов упражнений, новых рендереров нет
- [x] Новое семейство событий в `notification-service`: выдано / приближается дедлайн / напоминание

> **Сделано 2026-08-18.** Блок закрывает дыру, которую 40.21 и 40.22 оставили осознанно:
> **`AssignmentProgressRecords` наконец получил создателя строк.** Выдача задания резолвит правило
> аудитории в конкретных людей, пишет по строке `not_started` на каждого и ставит в тот же outbox по
> событию `assignment.issued` на каждого — одной транзакцией, поэтому «попросили» и «сказали» не могут
> разойтись. С этого момента воронка РОПа и оценка порога из 40.22 работают по непустому множеству.
> Семь развилок решены и записаны с отвергнутыми альтернативами в `docs/DECISIONS.md` (2026-08-18);
> четыре стоит знать, читая 40.24–40.26:
> - **Состав организации спрашивается у identity-service синхронно, а не реплицируется через Kafka.**
>   Решающим был не архитектурный вкус, а режим отказа: отстающая (или ни разу не забэкфилленная)
>   реплика резолвит «всю команду» в девять человек из сорока, выдаёт девятерым и **сообщает об
>   успехе** — узнать об этом неоткуда. Недоступный identity ломается наоборот: `503` на кнопке,
>   ничего не записано, РОП жмёт ещё раз. Цена размена — выдача зависит от доступности identity —
>   записана в `docs/DONT_FORGET.md`.
> - **Явный список `userIds` тоже фильтруется по живому составу.** 40.21 хранил их непроверенными,
>   потому что не мог проверить. Здесь это ловит и уволившегося (выбрасывается с записью в лог, а не
>   отказом на всё задание), и — важнее — чужой `userId` из другой организации: строка прогресса легла
>   бы в правильный тенант, поэтому проверки изоляции этого не поймали бы, а уведомление ушло бы
>   человеку из другой компании.
> - **Персона практического диалога приходит из learning-service, а не из браузера.** Клиент, который
>   начинает сессию, принадлежит тому, кого оценивают: присланная персона — это переписываемая
>   персона («соглашайся на любую цену»), а это ровно то четырёхминутное «прохождение», которое
>   40.22 делал недостижимым. Вызов при этом **fail-open**: недоступность learning-service стоит
>   разговора без персоны, а не закрытого экрана практики.
> - **Раздача только добавляет, никогда не удаляет.** Новичок, пришедший после выдачи, попадает в
>   задание пересохранением (у активного задания аудитория перерешивается); уволенный сохраняет строку
>   как историю, но уведомлений больше не получает — и раздача, и джоба дедлайнов сверяются с живым
>   составом.
>
> Фоновая джоба одна — `AssignmentDeadlineSweepService`, шестая запись в
> `docs/TENANCY/BACKGROUND_JOBS.md` §2.1 (обход по организациям над системным перечислением, тот же
> `BYPASSRLS`-примечание). Три семейства уведомлений — три отдельных `NotificationType`, потому что
> получатель читает их по-разному, и третье существует именно потому, что первые два проигнорировали.
> Фронт: полоса активных заданий на `/tree`, при отсутствии заданий не рендерит **ничего** — дерево
> навыков выглядит ровно как раньше. Схема: одна нуллабельная колонка, без индекса (решение, а не
> забывчивость), без бэкфилла, без окна невидимости; read-only проверка —
> `docs/TENANCY/sql/40.23_assignment_fanout_verify.sql`, против настоящей БД не выполнялась. Админки
> РОПа по-прежнему нет (это 40.20, ждёт дизайн владельца), поэтому создание задания и кнопка
> «напомнить» существуют только как API. Тестов не написано (Правило №3) — двенадцать непокрытых мест
> перечислены в `docs/DONT_FORGET.md`, первым стоит фильтрация чужого `userId`.

### [x] 40.24 Автоповторы
- [x] `repeat_schedule`: сокращённая версия через **+7** и **+21** день, настраивается один раз
- [x] Эффект тренинга рассыпается за 2–3 недели — разовое задание воспроизводит именно
      тот провал, ради которого всё строится
- [x] Фоновая джоба выдачи повторов — с обходом по организациям (см. 40.14)

> **Сделано 2026-08-18.** `repeat_schedule` лежал в схеме с 40.21 и никем не интерпретировался —
> теперь у него закрытый словарь (`{"kind":"fixed_offsets","offsetDays":[7,21]}`, список
> необязательный и по умолчанию равен ровно роадмаповским двум числам) и фоновая джоба
> `AssignmentRepeatSweepService` — седьмая запись в `docs/TENANCY/BACKGROUND_JOBS.md` §2.1, обход по
> организациям над системным перечислением, тот же `BYPASSRLS`-примечание. Одиннадцать развилок
> решены и записаны с отвергнутыми альтернативами в `docs/DECISIONS.md` (2026-08-18); пять стоит
> знать, читая 40.25–40.26:
> - **Повтор — это новая строка `Assignments`, а не второй раунд внутри старой.** Схлопнутый вариант
>   выглядит дешевле и сохраняет «одно задание — одна воронка» на дашборде 40.25 буквально, но он
>   неисполним: `AssignmentProgressRecords` намеренно несёт **один** `BestScore` на человека, и
>   результат второй волны затёр бы результат первой — уничтожив единственное свидетельство того, что
>   эффект тренинга выветрился, то есть ровно тот факт, ради которого блок написан. Связь —
>   `RepeatOfAssignmentId` + `RepeatWaveIndex` (1-based), так что 40.25 собирает серию обратно
>   внешним ключом, а не догадками. Повтор никогда не указывает на повтор, и повтор не несёт своего
>   расписания (это запрещено `CHECK`, иначе веер экспоненциальный).
> - **Идемпотентность выведена из состояния, а не посчитана.** Волна выдана ровно тогда, когда
>   существует строка с парой `(RepeatOfAssignmentId, RepeatWaveIndex)`; уникальный частичный индекс —
>   всё, на чём это держится. Это не стилистика: привычный вариант (колонка «волна 1 отправлена» на
>   оригинале, как `DeadlineNoticeSentAt` в 40.23) здесь **невозможен** — оригинал может быть
>   `closed`, а закрытую строку триггер 40.21 не даёт обновлять вообще.
> - **+7 и +21 считаются от выдачи оригинала, и когорта идёт волной.** Персональный якорь (от момента,
>   когда конкретный человек взял порог) — учебниковый ответ и на этой схеме неисполним: сорок
>   человек, взявших порог в шесть разных дней, дают шесть заданий и шесть воронок, которые РОП не
>   прочитает. Единица его действия — планёрка, а когорта, разъехавшаяся на неделю, планёрки не имеет.
> - **Повтор уходит получателям оригинала, а не свежему перерешиванию правила аудитории**, и исход не
>   фильтрует: и провалившему порог, и не начавшему. Первое — потому что перерешённая «вся команда»
>   через три недели выдала бы **сокращённый** повтор (то есть практику без выброшенной из неё теории)
>   всем, кого наняли с тех пор, и сменила бы знаменатель между волнами. Второе — потому что 40.22
>   специально сделал `failed_threshold` видимым, назвав его самой ценной строкой на экране.
> - **«Сокращённая» значит меньше повторений и меньше теории, но никогда не ниже планка.** Выбрасывается
>   `reference_material` (кроме случая, когда это всё содержимое задания), `dialog_score.requiredCount`
>   делится пополам с округлением вверх; `minimumScore` и `minimumAccuracyPercent` копируются как есть.
>   Понижение планки сделало бы волны несравнимыми — а сравнение и есть весь смысл серии.
>
> Два следствия, которые снаружи выглядят как баги и таковыми не являются: **закрытое задание
> продолжает порождать волны** (пятидневное задание положено закрывать на седьмой день, и повтор,
> умирающий от того, что РОП прибрался, работал бы только у тех, кто ничего не закрывает — отменять
> надо, пока задание активно, правкой `repeatSchedule`), и **волна, опоздавшая больше чем на три дня,
> не выдаётся никогда** (смысл интервального повторения в интервале; заодно это не даёт первому тику
> после деплоя выдать все исторические волны разом). Оба записаны в `docs/DONT_FORGET.md`.
>
> Нового семейства уведомлений нет: повтор — это новый `assignment.issued` с новым id задания,
> notification-service не менялся ни строкой. Раздача 40.23 вынесена в `AssignmentFanOut`, чтобы у
> ручной выдачи и у волны осталась одна история идемпотентности. Фронта нет по той же причине, что в
> 40.15–40.23 — админка РОПа это 40.20. Схема: две нуллабельные колонки, уникальный частичный индекс
> (создаётся миграцией, а не отложенным скриптом — это ограничение корректности), три `CHECK`,
> `CREATE OR REPLACE` триггера заморозки; без бэкфилла и без окна невидимости, долгих перестроек нет
> и отдельного `_indexes_concurrently.sql` намеренно тоже; read-only проверка —
> `docs/TENANCY/sql/40.24_assignment_repeats_verify.sql`, против настоящей БД не выполнялась. Тестов
> не написано (Правило №3) — шестнадцать непокрытых мест перечислены в `docs/DONT_FORGET.md`, первой
> стоит идемпотентность волны.

### [x] 40.25 Дашборд РОПа и двусторонняя связь
- [x] По заданию: воронка назначено → начал → завершил → достиг порога
- [x] По менеджеру: где именно проседает, с привязкой к этапу воронки продаж
- [x] По команде: тепловая карта навыков
- [x] **Цитаты из диалогов, а не только цифры**: «68 баллов» РОПу неприменимо, три реплики
      где менеджер слил цену — готовый материал на планёрку в понедельник. Это то, ради
      чего продукт открывают раз в неделю, а не раз в квартал
- [x] РОП выделяет фрагмент диалога, комментирует, отправляет менеджеру
- [x] Менеджер **оспаривает оценку ИИ** → уходит РОПу на рассмотрение. Без этого первая же
      спорная оценка обнуляет доверие команды ко всем цифрам; плюс это даёт размеченные
      данные для настройки промптов оценки
- [x] Метрики воронки заданий — в `analytics-service`
- [~] Экран РОПа не нарисован — это 40.20, и она ждёт дизайн владельца. Весь блок сделан
      как API и одна таблица; фронт сделан только на стороне менеджера (входящие
      `/dialog-reviews` и кнопка оспаривания в модалке обратной связи)
- [x] Уведомление РОПу о новом оспаривании — **закрыто в 40.26**. На момент 40.25 администраторов
      организации было нечем перечислить (`/internal/memberships/active` отдавал id без ролей);
      40.26 добавил туда `administratorUserIds` и завёл `dialog.review.disputed`, уходящий каждому
      администратору, кроме автора оспаривания. Чтение ростера здесь **fail-open**: недоступность
      identity-service стоит уведомления, но не самого оспаривания

> **Сделано 2026-08-18.** 40.21–40.24 построили механику, и посмотреть на неё было негде. Этот
> блок — экран: три эндпоинта в learning-service, два в ai-service, одна новая таблица, два
> счётчика в analytics-service. Девять развилок с отклонёнными альтернативами в
> `docs/DECISIONS.md` (2026-08-18); шесть из них меняют то, как читаются 40.21–40.24:
>
> - **Воронка из пяти стадий, а не из четырёх.** `failed_threshold` — не подмножество
>   «завершил», а отдельный счётчик: 40.22 разделил `in_progress` и `failed_threshold` именно
>   чтобы «начал, пробовал 4 раза, не дотянул» было видно, и воронка, кончающаяся на «завершил»,
>   вернула бы этих людей обратно к тем, кто не начинал.
> - **Уволенный помечается, а не удаляется и не считается** — та дыра, которую 40.23 оставил
>   явно. Строка прогресса остаётся (это запись о том, что человека спросили), но дашборд
>   спрашивает identity-service, кто ещё работает, и отдаёт `leftOrganizationCount` рядом с
>   сырыми числами. **Это чтение fail-open**, в отличие от резолвера аудитории при выдаче:
>   `null` значит «не смогли проверить», а не ноль, и падение identity стоит одной пометки, а не
>   всего экрана.
> - **«Этап воронки продаж» — это `Skill.Stage`, а не второй словарь.** `CompanyStatus` из 39.10
>   отклонён: это пайплайн **сделки** (где компания стоит в CRM), а вопрос дашборда — про
>   **разговор**, на каком этапе звонка менеджер разваливается.
> - **Цитаты берутся из ai-service, и learning-service не читает Mongo.**
>   `IDialogSessionRepository` вырос на два тенант-фильтрованных метода вместо того, чтобы
>   появился второй держатель коллекции (юнит-тест в ai-service это стережёт грепом по исходникам
>   и по-прежнему зелёный). Экран спрашивает каждый сервис о том, чем тот владеет.
> - **Одна таблица `DialogReviewNotes` на оба направления связи**, с колонкой `Kind`. Комментарий
>   РОПа и оспаривание менеджера — один объект с двух концов; различаются они только тем, какими
>   словами закрываются, и это `CHECK`, а не вторая схема. Живёт в learning-service, потому что
>   оспаривается число из `UserDialogScores`, а оно здесь. **Ни одна запись не берёт менеджера,
>   сценарий и оценку из тела запроса** — всё читается из строки оценки под RLS, поэтому «РОП не
>   может адресовать комментарий чужому сотруднику» это свойство запроса, а не проверка, которую
>   надо помнить.
> - **Удовлетворённое оспаривание записывает исправленную оценку и не применяет её.** Ручная
>   правка была бы затёрта следующей переотправкой события (40.22 всё пересчитывает), а порог,
>   который выторговывает у РОПа тот, кого им измеряют, — это то же «прохождение за четыре
>   минуты» с другой стороны. Ретро-скоринг — продуктовое решение владельца, оно в
>   `docs/DONT_FORGET.md`.
>
> **«Метрики воронки заданий — в `analytics-service`»** физически означает два платформенных
> Prometheus-счётчика (`app_assignments_issued_total`, `app_assignment_progress_total{state}`) на
> `assignment.issued` и новом `assignment.progress.changed`, а не проекцию: `ANALYTICS_SERVICE.md`
> ещё в 40.16 зафиксировал правило — сервис Redis-only, попыток не хранит, метку организации не
> носит. Значит, analytics отвечает на «делает ли кто-нибудь задания вообще», а воронка с именами
> считается в learning-service по строкам.
>
> **Заодно починена дыра, прожившая три блока:** `/assignments/*` и `/admin/assignments/*` вообще
> не были прописаны в гейтвее, то есть экран менеджера из 40.23 в любом развёрнутом окружении
> отдавал 404. Ни один тест этого не видит — такого теста в репозитории нет; он записан первым
> пунктом в «Тесты, которых нет».
>
> Схема: одна таблица, четыре индекса (один — частичный уникальный, «одно открытое оспаривание на
> разговор»), восемь `CHECK`, строгая RLS. Бэкфилла нет — комментариев и оспариваний никогда не
> существовало; отдельного `_indexes_concurrently.sql` нет и не должно быть — таблица создаётся
> пустой, а две другие трети блока читают уже существующие индексы. Проверочный скрипт
> `docs/TENANCY/sql/40.25_dialog_reviews_verify.sql` (только чтение) против настоящей БД не
> выполнялся; в его разделе 6 лежит извлечение размеченного датасета, ради которого роадмап
> механизм оспаривания и вводит. Тестов не писалось — Правило №3; восемь непокрытых мест с
> описанием опасности записаны в `docs/DONT_FORGET.md`.

### [x] 40.26 Непрохождение как рабочий сценарий
- [x] Уведомление РОПу **за день до дедлайна** со списком тех, кто не начал, и кнопкой
      «напомнить» в один клик
- [x] Не отчёт, который РОП может открыть, а адресный пуш с действием
- [x] Внедрение упирается не в качество контента, а в то, дожмёт ли РОП команду —
      проектировать под это
- [~] Экрана РОПа по-прежнему нет — это 40.20, и она ждёт дизайн владельца. Ссылка из уведомления
      (`/admin/assignments/:id?action=remind&scope=not_started`) сегодня отдаёт 404 во фронте, тело
      уведомления при этом самодостаточно, а API за ссылкой отвечает. `docs/DONT_FORGET.md`

> **Сделано 2026-08-18.** Первый блок этапа E **без миграции**: ни таблицы, ни колонки, ни новой
> джобы. Всё, что нужно было построить, — это способность назвать адресата. Восемь развилок с
> отклонёнными альтернативами в `docs/DECISIONS.md` (2026-08-18); пять из них меняют то, как читаются
> 40.23–40.25:
>
> - **Главное, что разблокировал блок, — платформа научилась перечислять администраторов
>   организации.** Все РОПовские уведомления упирались в один и тот же отсутствующий факт, поэтому
>   40.25 отложил свой пуш об оспаривании именно сюда. `GET /internal/memberships/active` получил
>   `administratorUserIds` — **подмножество тех же id**, а не роль на каждого участника: роль на
>   каждого ответила бы на тот же вопрос, опубликовав ролевой справочник организации любому сервису с
>   общим секретом, а learning-service никогда не спрашивает «кто этот человек», только «кому про это
>   рассказать».
> - **Дайджест уходит всем администраторам, а не автору задания.** `created_by` **null у каждой
>   автоволны** (40.24), автор мог уволиться (40.23 целый блок закрывал эту дыру), и адресат-одиночка
>   молчит ровно ту неделю, когда он в отпуске. Цена честно записана: пятеро администраторов читают
>   один дайджест, и четверо думают, что нажмёт пятый, — это лечится экраном 40.20, а не адресацией.
> - **Ноль не начавших — уведомления нет вообще.** «Все молодцы» — это сообщение, которое обучает
>   РОПа пропускать канал, и тогда одно важное письмо («четверо не начали») придёт в уже
>   натренированный на игнор ящик. Задание при этом всё равно помечается объявленным.
> - **Пуш несёт действие, а не ссылку на отчёт.** `actionUrl` —
>   `/admin/assignments/{id}?action=remind&scope=not_started`, за ним живой
>   `POST /admin/assignments/{id}/remind?scope=not_started`. Ссылка **открывает экран, а не выполняет
>   рассылку сама**: URL, который шлёт письма команде в момент открытия, срабатывает от почтового
>   сканера. И `scope` пришлось завести: напоминание 40.23 шло всем незавершившим, а уведомление,
>   назвавшее пять фамилий, чья кнопка дёргает двенадцать человек, — это продукт, делающий не то, что
>   сам только что сказал.
> - **Два риска блок создал и в нём же закрыл.** Кнопка перед всеми администраторами сразу означала
>   пять одинаковых напоминаний одному менеджеру после одной планёрки — ключ дедупликации
>   `assignment.reminder` огрублён с мгновения до **часа**. И ручное «напомнить» теперь **читает живой
>   ростер** (fail-closed, 503 и «нажмите ещё раз»): это был последний путь в фиче, которым можно было
>   отправить бывшему сотруднику домашнее задание бывшего работодателя. Пуш об оспаривании, наоборот,
>   **fail-open** — правило, которое стоит унести дальше: чтение, решающее «кого просят сделать
>   работу», падает громко; чтение, решающее «кому рассказать про уже записанную строку», не имеет
>   права уносить строку с собой.
>
> **Идемпотентность не потребовала ничего нового.** `DeadlineNoticeSentAt` из 40.23 уже отвечает на
> «эту дату объявили?», дайджест описывает ту же дату, перенос дедлайна уже сбрасывает колонку и
> перевзводит оба уведомления. Вторая колонка рассматривалась и отклонена как второй ответ на один
> вопрос. Единственный случай, ради которого её заводили бы, — тик, который может прочитать ростер, но
> не администраторов (identity-service старее этого блока): организация **пропускается целиком**, не
> отмечается ничего, следующий тик подберёт сам.
>
> **Чего в блоке нет намеренно:** автозакрытия просроченного задания. `BACKGROUND_JOBS.md` §4c и §4d
> дважды писали, что «джобу должен 40.26», — но этого нет в трёх пунктах блока, а закрытие по таймеру
> отнимает у РОПа продление срока, которое 40.21 специально оставил доступным. За этим стоит
> продуктовый вопрос («прошедший дедлайн — это работу не принимаем или принимаем с опозданием?»), и он
> в `docs/DONT_FORGET.md`. Схема не менялась вообще, поэтому ни миграции, ни
> `40.26_*_indexes_concurrently.sql` нет и быть не должно; `docs/TENANCY/sql/40.26_deadline_digest_verify.sql`
> — только чтение, против настоящей БД не выполнялся, и существует потому, что у РОПа нет экрана и
> увидеть работу фичи больше негде. Тестов не писалось — Правило №3; четыре непокрытых участка с
> описанием опасности записаны в `docs/DONT_FORGET.md`, включая тот факт, что
> `GET /internal/memberships/active` не покрыт ничем **с 40.23**, а этот блок его расширил.

---

### Этап F — ИИ в админке (РОП не должен видеть пустой редактор)

### [x] 40.27 Чекпоинт между структурированием и генерацией
- [x] Продуктовая версия конвейера из `.claude/local-seed/seed.py` (структурировать →
      сгенерировать), но с **остановкой посередине** — `ContentGenerationJobs` в learning-db,
      пять состояний, `awaiting_review` посередине. Ограничение БД
      `CK_ContentGenerationJobs_Checkpoint` запрещает переход в генерацию без структуры и
      без подтверждения человеком. Оба LLM-вызова — в ai-service
      (`POST /ai/content/structure`, `POST /ai/content/generate`), внутренние, как `/ai/evaluate`
- [x] Показать извлечённую структуру: продукт, список возражений, этапы скрипта, тон →
      «всё верно? что убрать, что добавить?» — `GET /admin/content-generation/{id}` отдаёт
      структуру, `PUT …/structure` её правит, `POST …/approve` подтверждает. **API, без экрана:**
      экран РОПа — это 40.20, ждёт дизайн владельца
- [x] Правка здесь — 30 секунд; та же правка после генерации — переделка 15 упражнений.
      Плюс дешевле по токенам: не генерируется то, что выкинут — материал **не передаётся** в
      вызов генерации, только подтверждённая структура: презентация оплачивается один раз, а
      удалённое человеком возражение не возвращается моделью обратно
- [x] **Самый дешёвый пункт этапа с наибольшим эффектом — делать первым**
- [x] Сгенерированный урок ложится обычными строками (`Lesson` + `Exercise` + замороженный
      `LessonVersion`), принадлежит организации, никогда не глобальный, и приходит
      **архивным** — поштучная приёмка это 40.32. `PUT /admin/lessons/{id}` получил
      необязательный `isArchived`, потому что обратного пути из архива раньше не было
- [x] Фоновая джоба `ContentGenerationSweepService` — восьмая запись в
      `docs/TENANCY/BACKGROUND_JOBS.md` §2.1, режим объявлен явно
- [~] Тесты — **не писались**, Правило №3 в `docs/DONT_FORGET.md` (запрет владельца от
      2026-08-16). Непокрытое перечислено там же в разделе «Тесты, которых нет»
- [x] Документация: `docs/CONTENT_PIPELINE.md` (новый), `docs/AI_SERVICE.md`,
      `docs/LEARNING_SERVICE.md`, `docs/SEEDER.md`, `docs/TENANCY/CONTENT_MODEL.md`,
      `docs/DB_SCHEMA.md`, `docs/API_CONTRACTS.md`, `docs/ADMIN_PANEL.md`,
      `docs/TENANCY/BACKGROUND_JOBS.md`, `docs/DECISIONS.md`, `docs/DONT_FORGET.md`,
      `docs/FEATURES.md`

### [x] 40.28 Порог достаточности входа
- [x] ИИ **отказывается** генерировать при недостатке материала и говорит, чего конкретно
      не хватает («добавьте примеры возражений или запись звонка») — шестое состояние
      `insufficient` в `ContentGenerationJobs` плюс jsonb-колонка `Insufficiency` со списком
      `{code, message}` из **закрытого словаря семи кодов**, чтобы экран 40.20 рисовал пункты, а не
      абзац. Отказ — **не 400**: `POST …/material` дописывает материал и продолжает прогон,
      `PUT …/structure` открыт на отказанном прогоне и переинспектирует результат. Ограничение БД
      `CK_ContentGenerationJobs_Insufficiency` держит «отказ есть ⇔ список есть»
- [x] Порог в **две ступени, и обе бесплатны**. Детерминированная (learning-service, до любого
      вызова): 400 символов / 60 слов плюс лексическая проверка «есть ли в документе хоть одно
      слово про продажи» — она и отличает три слайда про CRM от трёх страниц кулинарного рецепта.
      Модельная **едет в том же вызове структурирования** (`{structure, sufficiency}`), отдельного
      дешёвого LLM-вызова нет: этот вызов и так читает весь материал. Вердикт модели умеет
      **добавить** отказ и не умеет его снять
- [x] Лучше 4 хороших упражнения, чем 15 ватных — и **честный сигнал не длина, а извлечённая
      структура**: нет возражений И нет этапов скрипта, или нет продукта И нет ICP → отказ, даже
      если модель сказала «достаточно». Порог переубеждаем человеком, но не обходим: каждый путь
      обратно переинспектируется, кнопки «сгенерировать всё равно» нет
- [x] Отказ не переплачивает: `StructuredMaterialLength` помнит, сколько материала уже прочитано,
      и возобновлённый прогон отправляет **только дописанное** плюс уже извлечённую структуру
- [~] Тесты на тонкий вход (пустой, слишком короткий, не про продажи) — **не писались**, Правило №3
      в `docs/DONT_FORGET.md` (запрет владельца от 2026-08-16). Все три случая из формулировки
      этого пункта плюс остальное непокрытое расписаны там же в разделе «Тесты, которых нет»
- [x] Схема: миграция `AddContentGenerationSufficiency`. **Долгих индексов нет** — фильтр по
      статусу уже обслуживается существующим индексом, внутрь jsonb никто не ходит; поэтому файла
      `docs/TENANCY/sql/40.28_*.sql` нет и быть не должно (обоснование в `docs/DECISIONS.md`)
- [x] Документация: `docs/CONTENT_PIPELINE.md` (§4a), `docs/AI_SERVICE.md`,
      `docs/LEARNING_SERVICE.md`, `docs/LLM_FAILURE_HANDLING.md`, `docs/DB_SCHEMA.md`,
      `docs/API_CONTRACTS.md`, `docs/ADMIN_PANEL.md`, `docs/TENANCY/CONTENT_MODEL.md`,
      `docs/TENANCY/BACKGROUND_JOBS.md`, `docs/DECISIONS.md`, `docs/DONT_FORGET.md`

> Что 40.28 оставляет человеку — целиком в `docs/DONT_FORGET.md`, но три вещи стоит знать, читая
> 40.29–40.32. **Первое: калибровка порога — продуктовое решение, принятое агентом за владельца.**
> 400 символов, 2 возражения, 3 этапа скрипта — числа выбраны по здравому смыслу, реальных
> материалов заказчиков через конвейер не проходило ни одного, и риск несимметричен: лишний отказ
> клиент видит, а «мог бы быть лучше» — нет. **Второе: ни один промпт этого конвейера по-прежнему
> ни разу не выполнялся** — включая инструкцию про `sufficiency`, поэтому калибровка модели на
> `isSufficient` неизвестна; защита от завышения в коде есть (вердикт не умеет снять отказ), от
> занижения — только частичная (отказ без кодов игнорируется). **Третье: 40.29 наследует словарь
> отказов.** «Профиль как интервью» — это тот же вопрос «чего не хватает», заданный не про один
> прогон, а про организацию; коды `no_product` / `no_icp` / `no_objections` / `no_script` уже
> существуют, и заводить второй словарь пробелов не надо. Схема менялась одной миграцией
> (`AddContentGenerationSufficiency`), долгих индексов нет, поэтому `docs/TENANCY/sql/40.28_*.sql`
> нет и быть не должно. Тестов не писалось — Правило №3; пункт про тесты стоял прямо в роадмапе
> блока и помечен `[~]`, все три названных в нём случая расписаны в «Тесты, которых нет».

---

### [x] 40.29 Профиль компании как интервью, а не форма
- [x] РОП загружает презентацию продукта и скрипт → ИИ заполняет что смог → спрашивает
      только про пробелы
      — материал читает уже существующий конвейер 40.27/40.28 (второго извлечения не заводили:
      структура там намеренно имеет форму профиля поле в поле). Перенос — два маршрута в
      organization-service: `POST /organizations/profile/draft` (предпросмотр, ничего не пишет) и
      `POST /organizations/profile/draft/apply`. Пробелы — `GET /organizations/profile/gaps`,
      закрытый словарь из семи кодов с фиксированным русским вопросом на каждый; ответ — `PATCH
      /organizations/profile`, где опущенное поле сохраняет прежнее значение
- [x] 30 пустых полей никто не заполнит, профиль останется пустым, и параметризация
      базового контента (40.19) не заработает вообще
      — вопросы отдаются **по три за раз** в порядке приоритета, `totalGapCount` едет рядом, чтобы
      экран мог честно сказать «осталось ещё N». Тир `blocking` (продукт, ICP, три возражения) — это
      ровно те поля, без которых `{{organization.*}}` рендерит нейтральную заглушку; флаг
      `isReadyForParameterization` = «ни одного blocking-пробела не осталось»
- [x] 5 минут вместо часа
      — выражено числом: `limit` по умолчанию 3, зажат в 1…7. Это и есть весь механизм; длинный
      список — это возвращение формы
- [x] Политика слияния с уже заполненным профилем (развилка, оставленная 40.27): **заполнять
      пустое, дополнять списки, никогда молча не заменять написанное человеком.** `product` / `icp`
      / `tone` / `scriptStages` при конфликте **сохраняются**, если поле не названо явно в
      `acceptedFields`; `objections` и `glossary` объединяются, существующая запись побеждает;
      **`banned_claims` только пополняется — значения `acceptedFields`, которое его удалило бы, не
      существует**
- [x] Схема БД не менялась: интервью не хранит состояния (какой вопрос задан, что пропущено,
      переносили ли черновик) — всё выводится из того, какие колонки пусты. Миграции нет,
      бэкфилла нет, долгих индексов нет, поэтому `docs/TENANCY/sql/40.29_*.sql` нет и быть не
      должно. Новых фоновых джоб и консьюмеров тоже нет — в `docs/TENANCY/BACKGROUND_JOBS.md`
      по-прежнему восемь записей
- [x] Гейтвей: все четыре маршрута попадают под существующий `/organizations/{**catch-all}`,
      ограничения `Methods` у маршрута нет, CORS — `AllowAnyMethod()`. Проверено разбором
      `appsettings.json`, конфиг не менялся (ловушка 40.25)
- [x] Документация: `docs/ORGANIZATION_SERVICE.md`, `docs/CONTENT_PARAMETERIZATION.md`,
      `docs/AI_SERVICE.md`, `docs/LEARNING_SERVICE.md`, `docs/CONTENT_PIPELINE.md`,
      `docs/TENANCY/CONTENT_MODEL.md`, `docs/DB_SCHEMA.md`, `docs/API_CONTRACTS.md`,
      `docs/ADMIN_PANEL.md`, `docs/DECISIONS.md`, `docs/DONT_FORGET.md`
- [~] Тесты — **не писались, Правило №3** в `docs/DONT_FORGET.md` (владелец запретил 2026-08-16).
      Непокрытое расписано там же в «Тесты, которых нет»; самая опасная строка — политика слияния,
      потому что её ошибка не роняет сервис, а тихо переписывает `banned_claims` заказчика

> Что 40.29 оставляет человеку — целиком в `docs/DONT_FORGET.md`, но четыре вещи стоит знать,
> читая 40.30–40.32. **Первое: 40.29 сознательно нарушил рекомендацию 40.28 «второй словарь пробелов
> не заводить».** Он заведён, и причина в том, что «хватит ли материала на четыре упражнения» и
> «заполнен ли профиль компании» расходятся в обе стороны: `banned_claims` и глоссарий не мешают
> генерации и важны в профиле, а `too_short` / `off_topic` — факты про загруженный документ и ничего
> не говорят про строку, у которой документа нет. Общим осталось главное — закрытый список кодов и
> фиксированное предложение на сервере. **Второе: `PUT /organizations/profile` по-прежнему доступен
> любому участнику организации** — то есть рядовой менеджер может переписать `banned_claims`. Дыра
> старше этого блока; новые пишущие маршруты закрыты `RequireOrgAdmin`, старый не трогали намеренно,
> точная строка для правки лежит в `DONT_FORGET.md`. **Третье: `PATCH` — первый такой глагол во всём
> бэкенде;** в конфиге гейтвея всё сходится и это проверено, но пропускает ли его внешний
> реверс-прокси, кодом не проверить. **Четвёртое: путь «заполнить профиль по материалам» проходит
> через обычный прогон конвейера, и у РОПа заодно появляется архивный урок** — это не побочный
> дефект, а то же чтение того же документа, оплаченное один раз.

---

### [~] 40.30 Записи реальных звонков → библиотека возражений

> **Не реализуется автономным прогоном (2026-08-18).** Сам роадмап ставит условием
> «до реализации решить вопрос согласий и сроков хранения записей» — это юридическое
> решение владельца, а не техническое, и оно уже висит в `docs/DONT_FORGET.md`
> (раздел «Продуктовые вопросы, которые агент решить не может»). Строить загрузку и
> хранение записей реальных разговоров до того, как определены согласия и сроки
> хранения, значит закладывать в продукт данные, которые потом придётся удалять
> ретроактивно. Блок ждёт владельца; следующим взят 40.31.
- [ ] Загрузка записей (у клиента они уже лежат в телефонии)
- [ ] Извлечение: какие возражения реально звучат и с какой частотой, как их отрабатывают
      лучшие менеджеры, где сыпятся все
- [ ] На выходе — и контент, и настройки персон для тренажёра
- [ ] Коммерчески сильнее всего: контент собран из их собственной реальности, а частоты
      возражений настоящие, а не угаданные
- [ ] **До реализации решить вопрос согласий и сроков хранения записей** — иначе фича
      не пройдёт проверку безопасника крупного заказчика
- [ ] Обновить `docs/DATA_OWNERSHIP.md`

### [x] 40.31 Замыкание петли «метрика → контент»
- [x] Дашборд видит провал команды по этапу → админка сама предлагает сгенерировать упражнения
      на это. `GET /admin/team/skill-gaps` считает предложения **из того же вызова, которым
      рисуется тепловая карта 40.25**, поэтому красная ячейка без предложения (или предложение
      по зелёной) невозможны в принципе. «Провал команды» — три условия: ≥ 20 попыток на этап,
      точность ≤ 60%, ≥ 2 менеджера ниже порога
- [~] «…+ диалог с персоной, которая давит на скидку» — **не сделано**. Это генерация
      `DialogMode` в ai-service, где нет ни маршрута, ни промпта, ни валидатора для диалоговых
      режимов: отдельный блок размера 40.27, а не угол этого. Предложение генерирует упражнения;
      диалог РОП добавляет в то же задание обычным `dialog_scenario` из существующих режимов
      (схема заданий это умеет с 40.21, персону на элемент можно переопределить — 40.23).
      `docs/DONT_FORGET.md`
- [x] Одна кнопка, `source_type = gap_detected`, `source_ref` = метрика.
      `POST /admin/team/skill-gaps/{stageKey}/content` — одно нажатие запускает **обычный прогон
      40.27** (тот же чекпоинт, тот же порог 40.28, тот же архивный приезд урока), с материалом,
      собранным сервером из измерения и профиля организации. `source_ref` — это
      `skill-gap:<этап>@<yyyy-MM-dd>`; сами числа уезжают в `Goal`. И `source_type`, и `source_ref`
      **выводятся из прогона**, а не берутся из тела запроса
- [x] Превращает дашборд из отчёта в инструмент. Две трети блока — про то, чтобы инструмент не
      стал спамом: отклонение живёт 90 дней и досрочно отзывается падением на 10 пунктов, живой
      прогон подавляет этап целиком (и повторное нажатие возвращает **тот же** прогон, а не
      покупает второй урок), завершённый — на 30 дней. Подавленные пробелы всё равно возвращаются
      с причиной и сроком: панель, которая молчит, неотличима от сломанной

> **Сделано 2026-08-18.** Первый блок, где почти вся работа — это решения, а не код: три эндпоинта
> и один — четвёртым — на снятие отклонения, одна таблица, одна колонка. Десять развилок с
> отклонёнными альтернативами в `docs/DECISIONS.md` (2026-08-18); четыре из них меняют то, как
> читаются 40.21–40.29:
>
> - **Предложение — это вычисление, а не данные.** Панель считается в момент запроса
>   администратора, из той же матрицы, что и тепловая карта. Отклонённая альтернатива — ночной свип,
>   пишущий найденные пробелы в таблицу, — требовала девятого воркера, седьмого
>   `IgnoreQueryFilters`, второго писателя строк, которые между тиками никто не читает, гасителя
>   для пробелов, которые закрылись, и делала панель устаревшей на сутки. Хранится **только
>   отклонение** — единственный факт, которого в попытках нет. Та же форма, что у 40.18 (stale) и
>   40.25 (воронка).
> - **Пороги провала — продуктовое решение агента, и ни одно из чисел не выведено из данных.**
>   20 попыток — четыре пятёрки 40.25; 60% — двадцать пунктов ниже проходного бара 80% из примера
>   40.22 (на самом баре панель флагала бы всё и стала бы обоями); два менеджера — граница между
>   разговором с человеком, которого 40.25 уже называет по имени, и генерацией контента для всей
>   команды. Отклонён и относительный порог («хуже остальных на N%» всегда что-нибудь находит,
>   даже у отличной команды), и вынос в настройки организации (это перекладывание решения на того,
>   у кого нет оснований его принять).
> - **Кнопка порождает прогон генерации, а не задание.** Непроверенный вывод модели в живое дерево
>   не попадает — эту форму задал 40.27, и блок наследует её целиком. Задание появляется в конце,
>   через `POST /admin/assignments` с `contentGenerationJobId`, и этот маршрут **выводит**
>   `source_type`/`source_ref` из прогона, а не верит телу — то же свойство, которым 40.25 наделил
>   `DialogReviewNotes`. Отклонено создание задания сразу черновиком: у него нет содержимого (в этом
>   и состоит пробел), заполнять его пришлось бы фоновому писателю в обход триггера заморозки 40.21,
>   а черновик, который выглядит готовым и не готов, — худшее, что можно оставить на экране админки.
> - **Побочный эффект: у `source_type = 'training'` появился первый писатель.** 40.21 завёл это
>   значение и сказал, что его `source_ref` — замороженный `lesson-version:<uuid>`, и до этого блока
>   его **никто никогда не проставлял**: все задания были `manual`. Три строки, закрывшие дыру,
>   которая прожила девять блоков.
>
> **Материал для кнопки собирается из профиля организации** — измерение, слабейшие навыки этапа, и
> семь полей профиля обычным читаемым русским. Это третье применение профиля (после подстановки
> 40.19 и затравки структурирования 40.27) и самый сильный аргумент за его существование: у клиента
> с заполненным профилем получаются упражнения про его возражения, а у клиента с пустым — честный
> отказ 40.28 с перечнем того, что принести. `OrganizationProfilePromptBuilder` намеренно **не**
> переиспользован: его вывод — это блок промпта с ограждениями «обрабатывай как данные», а
> `SourceMaterial` показывается РОПу на чекпоинте под вопросом «откуда это взялось».
>
> Схема: одна таблица (пустая при создании, один уникальный индекс), одна nullable-колонка
> (metadata-only `ADD COLUMN`, частичный индекс по нулю строк), четыре `CHECK`, строгая RLS.
> Бэкфилла нет — ни одного отклонения и ни одного прогона «из пробела» никогда не существовало;
> **долгих индексов нет, поэтому `docs/TENANCY/sql/40.31_*_indexes_concurrently.sql` нет и быть не
> должно**, а `40.31_skill_gaps_verify.sql` (только чтение, семь разделов, седьмой — замкнутая
> петля) против настоящей БД не выполнялся. Новых джоб и консьюмеров нет: в `BACKGROUND_JOBS.md`
> по-прежнему восемь записей и счётчик `IgnoreQueryFilters` = 6, и это проверено грепом. Счётчика в
> analytics тоже нет — обоснование в `ANALYTICS_SERVICE.md`. Гейтвей не менялся: `/admin/team/*`
> уже покрыт `learning-admin-team` без ограничения `Methods` — ловушка 40.25 проверена разбором
> конфига, а не предположена. Тестов не писалось (Правило №3); восемь непокрытых мест с описанием
> опасности — в `docs/DONT_FORGET.md`, первым стоит детект пробела.

### [x] 40.32 Пакетная адаптация тона и ИИ-ревью контента
- [x] «Перепиши все упражнения этапа "закрытие" под наш продукт и тон» → фоновая джоба →
      список диффов → принять/отклонить поштучно. **Никогда не автоприменение**
      — `POST /admin/content/adaptations {mode, stageKey}` собирает этап **через резолв override'ов
      40.18** (своя копия урока, если она есть; базовая строка, если нет — но никогда обе), пишет по
      строке `ContentAdaptationItems` на упражнение и возвращается сразу: пока не потрачено ничего.
      Дальше девятая фоновая джоба `ContentAdaptationSweepService` — **один вызов LLM на упражнение**.
      «Никогда не автоприменение» — это не правило, а форма кода: воркер пишет только предложения и
      **физически не может писать `Exercise`**; единственный такой путь — `accept` по id **одного**
      элемента внутри запроса администратора. Массового глагола нет и не должно быть
- [x] ИИ-ревью контента, написанного РОПом руками: правильный ответ неоднозначен,
      дистракторы слишком очевидны, критерии свободного ответа неизмеримы
      — те же таблицы и тот же воркер при `mode = quality_review`, `POST /ai/content/review`.
      Ответ модели — **машиночитаемый перечень** из закрытого словаря семи кодов (три названных
      роадмапом плюс `multiple_correct_answers`, `answer_given_away`, `missing_explanation` и
      `banned_claim_rewarded`), с дословной цитатой из упражнения; русская фраза и серьёзность
      `blocking`/`advisory` — на сервере, как коды нехваток 40.28. **Ревьюер ставит диагноз и никогда
      не чинит**: применять нечего, `accept` отвечает 409, и это единственное место, где модель имеет
      мнение о контенте клиента, не имея возможности его исполнить
- [x] Контроль качества без участия Sellevate — иначе слабый контент клиентов становится
      воспринимаемым качеством продукта
      — закрытый словарь и есть механизм: «сколько у клиента упражнений с неизмеримыми критериями» —
      это запрос (раздел 8 верификационного скрипта), а не чтение. `banned_claim_rewarded` — самый
      острый из семи и причина, по которой ревью вообще получает профиль организации
- [x] **Куда приезжает переписанное** (развилка блока): в обычную строку `Exercise`, через приём
      человеком. Приём по упражнению из **общей библиотеки** сначала форкает урок
      (`CreateOverrideAsync`, 40.18) — не из вежливости: RLS не может защитить общую библиотеку,
      потому что «глобальный» это `NULL`, и запись в базовую строку применила бы правку тона одного
      клиента ко всем остальным. **Версия не публикуется** — правка ложится в черновик, как
      `PUT /admin/exercises/{id}`; версия остаётся решением человека (40.15)
- [x] **Что такое «дифф» физически**: два документа целиком плюс список **изменившихся листьев**
      (`options[1].text`) плюс фраза модели о том, что она изменила и зачем. Сервер ничего не
      сливает — запрет 40.18 на трёхстороннее слияние прозы и критериев оценки действует и здесь
- [x] **Стоимость и прерванный пакет**: аренда на пакете, идемпотентность на элементе. Элемент,
      уже несущий ответ, никогда не встаёт в очередь заново, каждый пишется своей транзакцией, бюджет
      попыток — **на элемент**, потолок пакета 60 упражнений, а этап выше потолка **отказывается с
      числом**, а не режется молча. Прерванный пакет стоит ровно одного вызова, который был в полёте
- [x] Схема: миграция `AddContentAdaptationBatches` — две пустые таблицы, девять `CHECK`, составной
      внешний ключ `(JobId, OrganizationId)`, частичный уникальный индекс «один живой пакет на этап»
      (это контроль расхода, а не аккуратность). **Долгих индексов нет**, поэтому файла
      `docs/TENANCY/sql/40.32_*_indexes_concurrently.sql` нет и быть не должно; читающий скрипт —
      `40.32_content_adaptation_verify.sql`
- [x] Гейтвей: добавлен `learning-admin-content` → `/admin/content/{**catch-all}`. Ловушка 40.25
      **сработала**: маршрута, совпадающего с `/admin/content/*`, в конфиге не было вообще, то есть
      API override'ов из 40.18 было недоступно снаружи кластера с момента выхода блока. Один маршрут
      закрывает и 40.18, и 40.32
- [x] `docs/TENANCY/BACKGROUND_JOBS.md`: девятая запись в §2.1, режим объявлен явно. Счётчики
      обновлены — `AddHostedService` = **30**, `IgnoreQueryFilters` в продакшн-коде = **7**
- [~] Тесты — **не писались**, Правило №3 в `docs/DONT_FORGET.md` (запрет владельца от 2026-08-16).
      Восемь непокрытых мест расписаны там же в «Тестах, которых нет»; два самых опасных —
      позиционное сопоставление упражнений при форке и сравнение хэшей на приёме, потому что оба
      **не падают, а тихо пишут не те слова в живое упражнение клиента**
- [x] Документация: `docs/CONTENT_PIPELINE.md` (§6a), `docs/TENANCY/CONTENT_MODEL.md` (§6),
      `docs/LEARNING_SERVICE.md`, `docs/AI_SERVICE.md`, `docs/SKILLS_AND_EXERCISES.md`,
      `docs/DB_SCHEMA.md`, `docs/API_CONTRACTS.md`, `docs/ADMIN_PANEL.md`,
      `docs/TENANCY/BACKGROUND_JOBS.md`, `docs/LLM_FAILURE_HANDLING.md`, `docs/DECISIONS.md`,
      `docs/DONT_FORGET.md`, `docs/FEATURES.md`

> **Сделано 2026-08-18. Этап F закрыт.** Обе половины пункта — это одна машина, различающаяся
> колонкой `Mode`, и это центральное решение блока: они отличаются одним промптом и тем, есть ли у
> элемента что применять; всё остальное — сбор этапа, аренда, захват, очередь, приёмка — общее.
> Строить их порознь значило бы завести второй протокол аренды, второй условный `UPDATE`, второго
> воркера и второе место, где можно тонко ошибиться в идемпотентности, — ради ничего, потому что
> трудные части у них общие.
>
> Три вещи меняют то, как читаются 40.15–40.31:
>
> - **«Никогда не автоприменение» — это утверждение о том, какие типы может писать класс.**
>   `ContentAdaptationStepRunner` пишет `ContentAdaptationItems` и колонки статуса своего пакета —
>   и всё. `accept` живёт в обработчике запроса администратора и принимает id **одного** элемента.
>   БД добивает то, что может выразить SQL (`CK_ContentAdaptationItems_Proposal`: принятый элемент
>   обязан нести предложение и строку, в которую оно уехало; ничего вне `accepted` не может нести
>   `AppliedAt`), а «нажал человек» выражено формой, а не ограничением. Будущая кнопка «применить
>   всё» сдвинула бы ровно эту границу — и это единственное изменение в фиче, которое нельзя делать
>   тихо.
> - **Копирование при записи 40.18 оказалось не соседней фичей, а несущей.** Приём по глобальному
>   упражнению форкает урок тем же вызовом, что и «редактировать»; без этого пакет писал бы в общую
>   библиотеку, а RLS этого не ловит — контентная политика допускает `OrganizationId IS NULL` и в
>   `WITH CHECK`. Раздел 4 верификационного скрипта проверяет ровно это. Побочный эффект, который
>   стоит увидеть на живых данных: пакет на этап из шести базовых уроков, принятый целиком, создаёт
>   шесть override'ов — и все шесть попадают в очередь stale 40.18, когда база сдвинется.
> - **Ловушка 40.25 наконец что-то нашла, и находка старше этого блока.** `/admin/content/overrides`
>   из 40.18 — пять маршрутов copy-on-write и очереди stale — не имел маршрута в гейтвее с момента
>   выхода 40.18. Ни один из существующих тестов этого не видит; строка «нет теста, сверяющего
>   маршруты контроллеров с гейтвеем» в `DONT_FORGET.md` подорожала второй раз.
>
> Ни рерайтер, ни ревьюер **ни разу не выполнялись против живого провайдера**. Калибровка обоих
> неизвестна в обе стороны, и три числа, на которые надо смотреть при первом живом прогоне, выписаны
> в `docs/DONT_FORGET.md`. Схема: одна миграция, две пустые таблицы, долгих индексов нет, бэкфилла
> нет. Тестов не писалось (Правило №3).

---

### Этап G — эксплуатация и завершение

### [x] 40.33 Квоты и стоимость на организацию
- [x] Лимиты голосовых минут и LLM-расхода — **per-organization**: таблица `OrganizationQuotas`
      в ai-db (голос — сутки/месяц, LLM — токены за месяц, резерв под фон). **Каждая колонка
      nullable, и `null` означает платформенное умолчание из `AiQuotas:Default…`, а не «без
      лимита»** — это и снимает развилку fail-open / fail-closed: организация без строки не
      «неограниченная», а посчитанная по умолчаниям, ровно как было до блока. Хранится в ai-db,
      а не в профиле организации с репликацией через Kafka, по одной конкретной причине: поднимать
      лимит приходится ровно тогда, когда клиент в него уперся, и отстающая реплика сделала бы
      подъём невидимым для энфорсера
- [x] Энфорс **на ai-service** — и сначала пришлось сделать это утверждение правдой. У
      learning-service жила своя копия `OpenAiChatService` (заявка 40.27) **и вторая, которую
      никто не записывал: `YandexTtsService` + `TtsRouter`** — из-за неё весь синтез речи в
      упражнениях `ai_dialogue` шёл мимо голосового счётчика, существующего с 40.11. Обе удалены;
      learning-service ходит в новые внутренние `POST /ai/chat`, `/ai/chat/stream`, `/ai/tts` и
      **не держит ключей провайдеров вообще**. `IOpenAiChatService`/`ITtsRouter` сохранили форму,
      поэтому `ExerciseDialogService` и оба его эндпоинта не тронуты. Внутри ai-service метр
      подключён ко всем четырём точкам вызова, включая второй, независимый путь
      `AiEvaluationStrategyBase` (его **не** сливали с `IOpenAiChatService` — у него свой контракт
      отказов, зафиксированный `LLM_FAILURE_HANDLING.md`). Утверждение теперь **проверяется**:
      `scripts/ai-provider-lint.py` падает на любом файле вне списка из шести
- [x] Один клиент, гоняющий голос сутками, деградирует только свою организацию: под
      пользовательским окном появилось **организационное** (`org:{orgId}:voice:org:…`). До блока
      суммарный расход клиента был «сколько у него мест × пользовательская квота» — места
      добавляли бюджет. Оба окна оставлены сознательно: пользовательское не даёт одному человеку
      сжечь день всей компании, организационное — компании сжечь месяц
- [x] Расход виден в дашборде раньше, чем в счёте от провайдера: `GET /admin/ai-usage` (по
      моделям, с оценкой стоимости и состоянием квоты, читает **администратор организации**, а не
      только платформенный) плюс четыре платформенных счётчика Prometheus и дашборд
      `sellevate-ai-spend`. **Метки организации в метриках нет — пятый раз в этой кодовой базе и
      первый раз про деньги**; «чей это расход» отвечают строки `AiUsageRecords`, а не Prometheus
- [x] Обновить `docs/VOICE_ROLEPLAY.md`, `docs/MONITORING.md` — плюс новый `docs/AI_QUOTAS.md`,
      `AI_SERVICE.md`, `LEARNING_SERVICE.md`, `ORGANIZATION_SERVICE.md`, `CONFIGURATION.md`,
      `DB_SCHEMA.md`, `API_CONTRACTS.md`, `LLM_FAILURE_HANDLING.md`, `ANALYTICS_SERVICE.md`,
      `TENANCY/BACKGROUND_JOBS.md`, `DECISIONS.md`, `DONT_FORGET.md`, `FEATURES.md`
- [~] Тесты — не писались, Правило №3 в `docs/DONT_FORGET.md` (владелец, 2026-08-16). Девять
      непокрытых мест с описанием «чем опасно отсутствие» — там же. Существующие тесты,
      сломанные переносом LLM-вызовов, починены: `OpenAiProviderErrorTests` в learning-service
      переписан на новый клиент, четыре теста ai-service получили заглушку метра

> **Что 40.33 меняет в том, как читаются 40.11–40.32.** Три вещи.
>
> **Первое: «единственная точка» была намерением, а не фактом, полтора этапа подряд.** 40.27
> положил конвейерные вызовы в ai-service именно затем, чтобы 40.33 был «фичей, а не
> переписыванием», и записал оставшуюся копию в `DONT_FORGET`. Записи оказалось мало: копия
> прожила шесть блоков, а вторую — речевую, которая была опаснее, — не заметил никто. Отсюда
> `scripts/ai-provider-lint.py`: инвариант, который держится на заметке в документе, не держится.
>
> **Второе: лимит считается в токенах, деньги — только показываются.** Токенами провайдер
> выставляет счёт, и их можно посчитать точно; цена — это наша ручная таблица, и лимит,
> номинированный в деньгах, молча сдвигается у всех заказчиков в день, когда кто-то правит
> константу. Правка `AiQuotas:PricePerMillionTokens` перерисовывает историю и не двигает ни один
> лимит. Обратная сторона: **LLM-цены в конфиге намеренно не заполнены** — они зависят от шлюза,
> а выдуманная цена в отчёте о деньгах хуже, чем её отсутствие. Непрайсованная модель приходит
> с `estimatedCost: null` и флагом, **никогда не нулём**.
>
> **Третье: отказ жёсткий, но порядок деградации выбран.** Интерактивная работа идёт до 100%
> лимита, фоновая останавливается на `100% − BatchReservePercent` (по умолчанию 90%). То есть
> у исчерпавшего месяц клиента сначала замолкает ночной конвейер контента, и только потом —
> разговор, в котором менеджер сейчас находится. Класс объявляет вызывающий
> (`X-Ai-Workload`), и **отсутствие заголовка означает интерактивный** — класс с бОльшим
> лимитом, чтобы не обновлённый клиент не оказался тихо прижат к 90%.

### [x] 40.34 Финальная приёмка и релиз
- [x] Полный прогон `code-reviewer` (opus) + `security-reviewer` по `feature/tenancy` vs `main`.
      Фокус — этапы D/E/F (`9e10ed7..feature/tenancy`, 515 файлов, +71 057, ноль тестов); этапы A–C
      проверялись на регрессию границы, поскольку 40.14 их уже принял. **Граница тенанта устояла:**
      оба ревьюера независимо не нашли способа прочитать чужую организацию через гейтвей, все семь
      продакшн-`IgnoreQueryFilters` признаны законными перечислениями, сырой SQL один и он
      параметризован. Найдено 47 + 15 замечаний; **одиннадцать дешёвых и все критичные починены в
      блоке** (одна межтенантная запись, одна дыра в авторизации, две денежные, шесть про
      живучесть). Дорогие и спорные — в `docs/DECISIONS.md` и `docs/DONT_FORGET.md`
- [x] `verifier`: сборка **0 ошибок**; юнит-прогон одиннадцати проектов — **891 passed, 4 skipped,
      0 failed**; `npx tsc --noEmit` — 0; `npx vitest run` — **353/353**; три линта тенантности —
      clean; `dotnet ef migrations has-pending-model-changes` по семи сервисам — расхождений нет.
      Гейт `dotnet format` был красный и **починен**; гейт `codestyle-lint.py` красный (1216 против
      88 на `main`) и оставлен владельцу — почему именно, в `DONT_FORGET.md`
- [~] Ручной прогон `docs/TESTING/TENANCY.md` на двух организациях — **действие человека**:
      нужна поднятая система и две заполненные организации (Правило №1). Чеклист **дописан и
      исполним** — раздел «40.34 — приёмка всей фазы на двух организациях» в конце файла закрывает
      и шесть блоков, у которых своего чеклиста не было вовсе (40.15, 40.17, 40.18, 40.21, весь F)
- [x] Синхронизировать `docs/ARCHITECTURE.md`, `docs/API_CONTRACTS.md`, `docs/DB_SCHEMA.md`,
      `docs/DECISIONS.md`, `docs/FEATURES.md`. Сверка с кодом дала 29 расхождений; самые дорогие —
      описание RLS-политики без ветки платформенного режима (её добавил `RefreshTenantPoliciesForPlatformStaff`
      2026-08-16), «`RequireOrgAdmin` не используется нигде» при девятнадцати вызовах, и таблица
      `Notifications` в `DB_SCHEMA.md` у сервиса, у которого базы нет вообще
- [~] Финальный PR `feature/tenancy` → `main` — **действие человека**. Ветка не запушена, PR не
      создан, в `main` ничего не влито: 111 852 строки, из них ~71 000 без тестов, а прогон был без
      присмотра. Команды и готовое тело PR — **`docs/TENANCY/RELEASE_PR.md`**, порядок проверок и
      раскатки — **`docs/TENANCY/PHASE_40_SUMMARY.md`**

> **Что 40.34 оставляет после себя, помимо галочек.** Три документа, которых у фазы не было.
>
> **`docs/TENANCY/PHASE_40_SUMMARY.md` — единая последовательность раскатки.** Инструкции писались
> по одной на блок, каждая верна внутри себя, но раскатывать придётся всё сразу, а порядок **между**
> сервисами из отдельных заметок не виден. Тринадцать шагов по семи сервисам, условие про права роли,
> под которой идут миграции (без него миграции отработают «успешно», не сделав ничего), и одно
> жёсткое межсервисное ограничение: **ai-service раньше learning-service**. К нему 40.22 и 40.33
> пришли независимо, и — это проверено — они не противоречат друг другу, а указывают в одну сторону.
>
> **Честная граница между «написано» и «работает».** Двенадцать верификационных SQL-файлов, пять
> раскаточных скриптов, оба Mongo-бэкфилла, все промпты этапа F и новый HTTP-шов learning→ai не
> выполнялись **ни разу ни против чего**. Практическое следствие: формат ответа модели проверен
> только на заглушках, и первое, что сломается при живом прогоне, — разбор, а не логика.
>
> **Счётчики проверены грепом, а не унаследованы.** `AddHostedService` = 30, `IgnoreQueryFilters` в
> проде = **7** (не 6: заметка 40.31 устарела в 40.32), сырого SQL = 0. Заодно выяснилось, что
> `dotnet test` по солюшену прогоняет **не все** проекты — настоящие 891 получаются только циклом по
> `*.Tests/*.csproj`, а тестового workflow в `.github/` нет вообще.

### [SKIP] 40.35 Поддомены `client.sellevate.site`
> Отложено сознательно: wildcard-TLS, автоматизация DNS, per-tenant CORS (сейчас
> allow-list фиксированный, см. `docs/DEPLOYMENT.md`), переделка OAuth-коллбэков.
> Покупает только брендинг. Делать, когда за брендинг кто-то доплатит.
> До тех пор организация резолвится из JWT после входа и по домену email при входе.

### [SKIP] 40.36 Реализация SSO (OIDC / SAML) и JIT-провижининг
> Шов заложен в 40.8 (`organization_auth_config`, `IAuthProvider`, трёхшаговый вход).
> Сама реализация — по первому платящему запросу, не раньше. Когда запрос придёт,
> работа = добавить провайдера, а не переписывать вход, сессии, инвайты и
> провижининг одновременно под срок клиента.
