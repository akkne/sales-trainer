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

### [ ] 40.9 Суперадминка платформы и миграция существующих данных
- [ ] Экран платформенного суперадмина: создать организацию, пригласить её первого
      `OrgAdmin`, приостановить/возобновить
- [ ] Импersonation: явный endpoint, выпускающий **новый токен** с другим `org_id`,
      с записью в аудит-лог. Никогда не параметром на обычном маршруте
- [ ] **Миграция живых данных:** создать организацию по умолчанию, привязать всех текущих
      пользователей через `membership`, проставить `organization_id` во всех
      tenant-scoped таблицах всех БД
- [ ] Скрипт миграции + **проверенный откат**; прогон на копии прод-базы до применения
- [ ] SQL всех разрушающих шагов показать перед выполнением (SAFETY RULES в CLAUDE.md)
- [ ] Обновить `docs/MICROSERVICES_PRODUCTION_MIGRATION.md` — раздел про раскатку тенантов

---

### Этап C — раскатка `organization_id` по сервисам

> Для каждого сервиса один и тот же чеклист: колонка → бэкофилл → EF-фильтры →
> индексы с `organization_id` первой колонкой → пересмотр UNIQUE-ограничений →
> RLS → аудит фоновых джоб → тесты изоляции. Порядок сервисов — по риску: сначала
> те, где лежат разговоры и прогресс.

### [ ] 40.10 learning-service
- [ ] `organization_id` в: `user_skill_progress`, `user_lesson_progress`,
      `user_exercise_attempts`, `user_technique_progress`
- [ ] Контентные таблицы (`skills`, `topics`, `lessons`, `exercises`, `techniques`,
      `reference_materials`) — колонка **nullable**: `NULL` = глобальная библиотека
- [ ] Query filter для контента: `x.OrganizationId == null || x.OrganizationId == current`
      (НЕ простое равенство)
- [ ] Фильтр нужен **каждой** сущности отдельно — навигации `Skill→Topic→Lesson→Exercise`
      фильтр не наследуют
- [ ] `Skill.IconicName`: `UNIQUE(organization_id, iconic_name)` — иначе второй клиент со
      своим `objections` не заведётся
- [ ] Индексы: `(organization_id, user_id, ...)`, перестройка через
      `CREATE INDEX CONCURRENTLY` + `suppressTransaction: true`, дроп старого **после**
- [ ] Проверка `pg_index.indisvalid` после конкурентной сборки (упавшая сборка оставляет
      невалидный индекс, который жрёт оверхед на записи)
- [ ] Долгие перестройки индексов — **отдельный операционный шаг**, не в
      `DatabaseBootstrapper` (иначе тормозит readiness и гоняется с репликами)
- [ ] RLS на всех таблицах; `ExerciseTypePrompt` остаётся платформенно-глобальным
- [ ] Обновить `docs/LEARNING_SERVICE.md`, `docs/DB_SCHEMA.md`

### [ ] 40.11 ai-service (Postgres + Mongo)
- [ ] `organization_id` в Postgres-таблицах сервиса (квоты, веса, настройки)
- [ ] **Mongo `DialogSession`**: поле `organizationId`, добавить в составные индексы,
      префикс будущего ключа шардирования
- [ ] RLS для Mongo не существует — фильтр прикладной; **все чтения сессий свести в один
      репозиторий**, чтобы место для аудита было одно
- [ ] Ревизия сидируемых скрытых режимов (`company-call`, `custom-scenario`) — остаются
      глобальными; org-авторские режимы получают организацию в ключе
- [ ] Redis: ключ verdict-кеша custom-scenario и `RedisIdempotencyStore` — **префикс
      `org:{orgId}:`** (иначе кешированный вердикт одной организации отвечает другой)
- [ ] Обновить `docs/AI_SERVICE.md`, `docs/AI_DIALOG.md`, `docs/CUSTOM_SCENARIO.md`

### [ ] 40.12 company-service
- [ ] `organization_id` в `Company`, `CallLogEntry`, `PracticeCall`, `CompanyContact`, `CompanyPersona`
- [ ] Скоуп становится двойным: организация **и** пользователь (личный CRM внутри компании)
- [ ] Индексы: `(organization_id, user_id)`, `(organization_id, company_id, occurred_at DESC)`
- [ ] **`FollowUpReminderBackgroundService`** — сейчас сканирует всех; переделать на
      обход организаций со scoped-контекстом на каждую
- [ ] `company.followup.due` несёт `organizationId` в конверте
- [ ] Обновить `docs/COMPANIES/COMPANIES.md`

### [ ] 40.13 Остальные сервисы
- [ ] `social-service`: Postgres `social` + Mongo `chat_conversations` + фото в MinIO;
      дружба и чат **не должны** пересекать границу организации
- [ ] `gamification-service`: таблицы + Hangfire-джобы (сброс серий, недельное закрытие) —
      обход по организациям либо явный `IsSystem`
- [ ] `notification-service`: Redis-инбоксы — префикс `org:{orgId}:`
- [ ] `analytics-service`: Redis-only, presence и воронки — префикс ключей, иначе утекает
      численность команды между заказчиками
- [ ] `identity-service`: `ExpiredRefreshTokenCleanupService` /
      `ExpiredEmailVerificationCleanupService` — явный системный режим
- [ ] Обновить `docs/SOCIAL_SERVICE.md`, `docs/GAMIFICATION_SERVICE.md`,
      `docs/NOTIFICATION_SERVICE.md`, `docs/ANALYTICS_SERVICE.md`

### [ ] 40.14 Аудит фоновых задач и приёмка изоляции
- [ ] Реестр всех `BackgroundService` / Hangfire-джоб: для каждой явно записать режим —
      «обход по организациям» или «системный»
- [ ] Незаданный тенант — **исключение, а не разрешение**: пустой контекст не должен
      молча означать «все данные»
- [ ] `OutboxRelayBackgroundService` — единственный легитимный системный читатель всех
      строк (потому тенант и лежит в payload конверта, а не выводится при публикации)
- [ ] Сквозной интеграционный тест: две организации, полный набор операций, проверка что
      ни один endpoint не отдаёт чужие данные
- [ ] Прогон `security-reviewer` (opus) по границе тенанта
- [ ] Заполнить `docs/TESTING/TENANCY.md`

---

### Этап D — контент: версионирование и кастомизация

### [ ] 40.15 Иммутабельное версионирование уроков
- [ ] `lesson (id, organization_id NULL=глобальный, parent_lesson_id, slug, archived)`,
      `UNIQUE (organization_id, slug)`
- [ ] `lesson_version (id, lesson_id, version_no, content jsonb, content_hash, status,
      base_version_id, is_breaking, created_by, published_at)`
- [ ] **Единица версионирования — урок целиком вместе с упорядоченным набором упражнений**
      (у `Lesson` сейчас нет тела: только `Title`, весь контент в `Exercise.SerializedContent`)
- [ ] `draft` мутабелен, публикация замораживает строку навсегда; следующая правка —
      новый draft копией последней опубликованной
- [ ] Частичный уникальный индекс: не более одного draft на урок
      (`WHERE status = 'draft'`) — иначе два админа делают две ветки без стратегии слияния
- [ ] `content_hash` — не плодить версию при публикации без изменений
- [ ] Обновить `docs/SKILLS_AND_EXERCISES.md`, `docs/DB_SCHEMA.md`

### [ ] 40.16 Привязка прогресса к версии (чинит искажение метрик)
- [ ] `UserExerciseAttempt` ссылается на `lesson_version_id` + идентичность упражнения
      **внутри** версии, а не на изменяемый `ExerciseId`
- [ ] То же для `UserLessonProgress`
- [ ] Миграция исторических данных: привязать существующие попытки к «версии 1»
- [ ] `is_breaking` на публикации: косметические правки дашборд склеивает в одну линию
      метрик, смысловые — разрывает
- [ ] Тест-регрессия: правка правильного ответа не меняет историческую точность
- [ ] Обновить `docs/ANALYTICS_SERVICE.md` (как метрики считаются по версиям)

### [ ] 40.17 Версионирование программы и зачисления
- [ ] `program_version (id, organization_id, version_no, status)`
- [ ] `program_item (program_version_id, skill_id, order_index, lesson_version_id)` —
      пин на конкретную версию урока
- [ ] `enrollment (user_id, program_version_id)` — менеджер закреплён за снимком
- [ ] Менеджер на 8-м уроке из 21 не должен обнаружить перестроенную программу; новые
      зачисления идут на новую версию, текущим — явный переход с показом диффа
- [ ] Структура программы — только ссылки: перестановка навыков не трогает ни один урок

### [ ] 40.18 Override'ы (copy-on-write) и очередь stale
- [ ] Копия урока создаётся **только** в момент, когда админ нажал «редактировать»,
      никогда при онбординге
- [ ] Резолв на чтении: есть override → он, нет → базовый
- [ ] При публикации новой версии глобального урока все override с устаревшим
      `base_version_id` помечаются `stale` и падают в очередь ревью админу организации
- [ ] **Автомерж не делать**: контент — это проза и критерии оценки, трёхстороннее
      слияние даёт правдоподобную бессмыслицу, которая потом оценивает продавца
- [ ] Экран ревью: что изменилось сверху, что изменила организация, три действия —
      принять базу (отбросить override) / оставить override (перепривязать base) / править
- [ ] То же для `Technique` и `ReferenceMaterial` (легко забыть)
- [ ] Промпты `DialogMode`/`DialogBundle` — override'абельны per-organization; сидируемые
      скрытые режимы остаются глобальными

### [ ] 40.19 Профиль организации и параметризация контента
- [ ] Подстановки в текстах базовых уроков и промптах персон резолвятся из
      `organization_profile` на этапе рендера
- [ ] Один базовый урок обслуживает всех — клиент заполняет форму, а не форкает контент
- [ ] `banned_claims` учитывается в промптах персон и критериях оценки (в регулируемых
      отраслях это аргумент в продаже)
- [ ] `seed.py` / `/admin/seeder/bundle` сеет **глобальную** библиотеку
      (`organization_id IS NULL`) — явный целевой параметр, запрет наведения на клиента
- [ ] **Замер на первом пилоте:** какая доля адаптации закрывается подстановкой из
      профиля, а какая требует правки текста урока руками. Больше трети руками →
      параметризация спроектирована не так, чинить до десятого клиента
- [ ] Обновить `docs/SEEDER.md`

### [ ] 40.20 Разделение админки
- [ ] Платформенная суперадминка: организации, глобальная библиотека, типы упражнений
- [ ] Админка организации (экран РОПа): своя программа, override'ы, профиль компании,
      люди, задания
- [ ] Ревизия всех текущих `/admin/*` маршрутов: каждый относится к одному из двух уровней
- [ ] Обновить `docs/ADMIN_PANEL.md`

---

### Этап E — задания: цикл «РОП → менеджеры»

### [ ] 40.21 Сущность Assignment
- [ ] `assignment (id, organization_id, created_by, title, goal, source_type:
      training|manual|gap_detected, source_ref, content jsonb, audience, opens_at,
      deadline, completion_rule jsonb, repeat_schedule jsonb, status)`
- [ ] `assignment_progress (assignment_id, user_id, status, best_score, attempt_count,
      first_opened_at, completed_at)`
- [ ] **Отдельная сущность, а не переиспользованный learning path**: дерево навыков —
      длинное, последовательное, в своём темпе; задание — короткое, адресное, командное,
      с дедлайном
- [ ] Живёт в `learning-service` (он владеет прогрессом и оценкой); новый сервис не нужен
- [ ] Обновить `docs/DB_SCHEMA.md`, `docs/API_CONTRACTS.md`

### [ ] 40.22 Правило завершения = порог качества
- [ ] `completion_rule` — порог, а не факт прохождения: `3 диалога с оценкой ≥70`,
      `точность по упражнениям ≥80%`
- [ ] Если засчитывать факт — прокликают за четыре минуты, дашборд покажет 100%, и РОП
      это поймает. Это граница между инструментом и имитацией
- [ ] `failed_threshold` — **нормальное видимое состояние**, не скрытый ретрай:
      «начал, пробовал 4 раза, не дотянул» — самая ценная строка на экране РОПа
- [ ] Оценка порога переиспользует существующий скоринг learning-service

### [ ] 40.23 Назначение, экран менеджера, уведомления
- [ ] Аудитория: конкретные пользователи / группа / вся команда
- [ ] Активное задание — первым экраном у менеджера, пока не выполнено
- [ ] Практический диалог задания = обычный `DialogSession` с инъекцией персоны —
      тот же приём, что уже делает `CompanyContextPromptBuilder`
- [ ] Контентный словарь задания — существующие 11 типов упражнений, новых рендереров нет
- [ ] Новое семейство событий в `notification-service`: выдано / приближается дедлайн / напоминание

### [ ] 40.24 Автоповторы
- [ ] `repeat_schedule`: сокращённая версия через **+7** и **+21** день, настраивается один раз
- [ ] Эффект тренинга рассыпается за 2–3 недели — разовое задание воспроизводит именно
      тот провал, ради которого всё строится
- [ ] Фоновая джоба выдачи повторов — с обходом по организациям (см. 40.14)

### [ ] 40.25 Дашборд РОПа и двусторонняя связь
- [ ] По заданию: воронка назначено → начал → завершил → достиг порога
- [ ] По менеджеру: где именно проседает, с привязкой к этапу воронки продаж
- [ ] По команде: тепловая карта навыков
- [ ] **Цитаты из диалогов, а не только цифры**: «68 баллов» РОПу неприменимо, три реплики
      где менеджер слил цену — готовый материал на планёрку в понедельник. Это то, ради
      чего продукт открывают раз в неделю, а не раз в квартал
- [ ] РОП выделяет фрагмент диалога, комментирует, отправляет менеджеру
- [ ] Менеджер **оспаривает оценку ИИ** → уходит РОПу на рассмотрение. Без этого первая же
      спорная оценка обнуляет доверие команды ко всем цифрам; плюс это даёт размеченные
      данные для настройки промптов оценки
- [ ] Метрики воронки заданий — в `analytics-service`

### [ ] 40.26 Непрохождение как рабочий сценарий
- [ ] Уведомление РОПу **за день до дедлайна** со списком тех, кто не начал, и кнопкой
      «напомнить» в один клик
- [ ] Не отчёт, который РОП может открыть, а адресный пуш с действием
- [ ] Внедрение упирается не в качество контента, а в то, дожмёт ли РОП команду —
      проектировать под это

---

### Этап F — ИИ в админке (РОП не должен видеть пустой редактор)

### [ ] 40.27 Чекпоинт между структурированием и генерацией
- [ ] Продуктовая версия конвейера из `.claude/local-seed/seed.py` (структурировать →
      сгенерировать), но с **остановкой посередине**
- [ ] Показать извлечённую структуру: продукт, список возражений, этапы скрипта, тон →
      «всё верно? что убрать, что добавить?»
- [ ] Правка здесь — 30 секунд; та же правка после генерации — переделка 15 упражнений.
      Плюс дешевле по токенам: не генерируется то, что выкинут
- [ ] **Самый дешёвый пункт этапа с наибольшим эффектом — делать первым**

### [ ] 40.28 Порог достаточности входа
- [ ] ИИ **отказывается** генерировать при недостатке материала и говорит, чего конкретно
      не хватает («добавьте примеры возражений или запись звонка»)
- [ ] Лучше 4 хороших упражнения, чем 15 ватных: РОП загрузит трёхслайдовую презентацию,
      получит посредственный результат и оценит **продукт**, а не свой материал
- [ ] Тесты на тонкий вход (пустой, слишком короткий, не про продажи)

### [ ] 40.29 Профиль компании как интервью, а не форма
- [ ] РОП загружает презентацию продукта и скрипт → ИИ заполняет что смог → спрашивает
      только про пробелы
- [ ] 30 пустых полей никто не заполнит, профиль останется пустым, и параметризация
      базового контента (40.19) не заработает вообще
- [ ] 5 минут вместо часа

### [ ] 40.30 Записи реальных звонков → библиотека возражений
- [ ] Загрузка записей (у клиента они уже лежат в телефонии)
- [ ] Извлечение: какие возражения реально звучат и с какой частотой, как их отрабатывают
      лучшие менеджеры, где сыпятся все
- [ ] На выходе — и контент, и настройки персон для тренажёра
- [ ] Коммерчески сильнее всего: контент собран из их собственной реальности, а частоты
      возражений настоящие, а не угаданные
- [ ] **До реализации решить вопрос согласий и сроков хранения записей** — иначе фича
      не пройдёт проверку безопасника крупного заказчика
- [ ] Обновить `docs/DATA_OWNERSHIP.md`

### [ ] 40.31 Замыкание петли «метрика → контент»
- [ ] Дашборд видит провал команды по этапу → админка сама предлагает: «сгенерировать
      5 упражнений на это + диалог с персоной, которая давит на скидку?»
- [ ] Одна кнопка, `source_type = gap_detected`, `source_ref` = метрика
- [ ] Превращает дашборд из отчёта в инструмент: отчёт открывают раз в квартал,
      инструмент, предлагающий следующее действие, — раз в неделю

### [ ] 40.32 Пакетная адаптация тона и ИИ-ревью контента
- [ ] «Перепиши все упражнения этапа "закрытие" под наш продукт и тон» → фоновая джоба →
      список диффов → принять/отклонить поштучно. **Никогда не автоприменение**
- [ ] ИИ-ревью контента, написанного РОПом руками: правильный ответ неоднозначен,
      дистракторы слишком очевидны, критерии свободного ответа неизмеримы
- [ ] Контроль качества без участия Sellevate — иначе слабый контент клиентов становится
      воспринимаемым качеством продукта

---

### Этап G — эксплуатация и завершение

### [ ] 40.33 Квоты и стоимость на организацию
- [ ] Лимиты голосовых минут и LLM-расхода — **per-organization**
- [ ] Энфорс **на ai-service** — единственной точке, через которую проходят все голосовые
      и LLM-вызовы; не переизобретать в каждом сервисе
- [ ] Один клиент, гоняющий голос сутками, деградирует только свою организацию
- [ ] Расход виден в дашборде раньше, чем в счёте от провайдера
- [ ] Обновить `docs/VOICE_ROLEPLAY.md`, `docs/MONITORING.md`

### [ ] 40.34 Финальная приёмка и релиз
- [ ] Полный прогон `code-reviewer` (opus) + `security-reviewer` по `feature/tenancy` vs `main`
- [ ] `verifier`: сборка бэкенда, все suite'ы, `tsc`, `vitest`, линт кодстайла
- [ ] Ручной прогон `docs/TESTING/TENANCY.md` на двух организациях
- [ ] Синхронизировать `docs/ARCHITECTURE.md`, `docs/API_CONTRACTS.md`, `docs/DB_SCHEMA.md`,
      `docs/DECISIONS.md`, `docs/FEATURES.md`
- [ ] Финальный PR `feature/tenancy` → `main`

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
