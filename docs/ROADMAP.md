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
### [x] Leagues — weekly leaderboard

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

## Phase 5 — Streaks & Gamification

### [x] Daily streak calculation job
- [x] Hangfire recurring job (runs daily at 00:05): for each user check `LastActivityDate`
  - if yesterday → streak continues (no-op, updated on exercise submit)
  - if > 1 day ago → reset `CurrentStreakDayCount` to 0
- [x] Update `ExerciseService` to set `LastActivityDate = today` and increment streak on submit
- [x] Expose `currentStreak` in Profile API response
- [x] Show streak on profile page

### [x] XP source tracking
- [x] `UserXp` records created on exercise completion (source: `exercise`)
- [x] Streak bonus XP on 7-day and 30-day milestones (source: `streak_bonus`)
- [x] Weekly XP totals fed into league standings

---

## Phase 6 — League Promotion

### [x] League promotion/demotion logic
- [x] `WeeklyLeagueClosureJob` — full logic implemented:
  - Top-10 in each league promoted to next tier
  - Bottom-5 demoted
  - New week: reset `weekly_xp`, create new `LeagueMembership` rows
- [x] Frontend league page shows promotion/demotion zone highlights
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
- [x] Completed: yellow circle `#FFC800`, gold medal badge top-right
- [x] Active: green circle `#58CC02`, `animate-ping` outer ring, popover card above node
- [x] Locked: gray `#F7F7F7` circle, lock icon SVG, `cursor-not-allowed`
- [x] Zigzag offset via `node-left` / `node-right` / `node-center` CSS classes
- [x] Popover card: skill title, mini progress bar, X/total lessons, "Старт" green button

### [x] StatsWidget redesign
- [x] 3 separate border cards: 🔥 Streak (yellow), ⚡ XP (blue), 🏆 XP Total (red)
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
- [ ] Redis leaderboard sorted set for league rankings

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
- [x] Session completion screen: XP earned, "Вернуться к пути" button
- [x] Hearts = 0 → failure screen with "Попробовать снова"
- [x] Restart session resets state without navigation

### [x] Wire popover → session tab
- [x] "Приступить к прохождению" navigates to `/session/[lessonId]`

### [x] Skip button in exercises
- [x] ПРОПУСТИТЬ button shown alongside ПРОВЕРИТЬ in all exercise types
- [x] Skip advances to next exercise without penalty or submission
- [x] Button only visible when `onSkip` prop provided (session context)

### [x] League countdown timer
- [x] "До конца недели Xд Xч" computed from `weekEndDate` and displayed above leaderboard
- [x] Updates every minute; shows minutes when < 1 hour remaining

### [x] Animated dashed path line
- [x] Active lesson segment uses SVG animated dashed green stroke (1.2s loop)
- [x] Completed segments stay solid green; inactive stays gray



## Phase 10 — Post-Session Statistics

> Реализовать статистику после прохождения урока.

### [x] Session result screen enhancements
- [x] Track session duration (start time → end time) via `useRef`
- [x] Track per-exercise correctness (correct / total) via `correctAnswerCount` state
- [x] Show on completion screen: time spent, accuracy %, XP earned, hearts remaining (2×2 grid)
- [x] `formatSessionDuration(seconds)` pure utility: "X сек" or "X мин Y сек"

---

## Phase 11 — Achievements & Badges

> Design source: project `16384358117617625529` — "Profile & Statistics (Vivid)" screen shows badges section.
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

### [x] Frontend — Badges on Profile
- [x] `useAchievements()` hook — fetches `/profile/achievements`
- [x] Badges 5-col grid on `/profile` page: locked (grayscale) vs unlocked (green border)
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
- [x] List of lesson cards: lesson number, title, description excerpt, status (locked/active/completed), XP reward
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

## Phase 18 — Achievement Unlock Notification

> После получения достижения показывать всплывающее уведомление (toast / modal) с анимацией.

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
> Profile currently shows streak/XP/achievements but lacks mastery %, league badge, and weekly XP target progress.

### [ ] Weekly XP target panel
- [ ] Backend: `GET /profile` returns `weeklyXpGoal` (configurable, default 600) and `weeklyXpCurrent`
- [ ] Frontend: progress bar "X / Y XP" with motivational subtitle on profile page
- [ ] Show "Top 5% learner" style motivational copy when near/over goal

### [ ] Mastery % stat
- [ ] Backend: compute mastery as % of completed exercises across all enrolled skills
- [ ] Expose `masteryPercent` in `GET /profile` response
- [ ] Show as 4th stat card on profile page (alongside streak, XP, league)

### [ ] League badge on profile
- [ ] Show current league tier name ("Ruby", "Elite", etc.) as a stat card on profile page
- [ ] Link from profile league card → `/league` page

---

## ~~Phase 20 — Leaderboard Podium (Top-3 highlight)~~ [SKIP]

> Design source: project `16384358117617625529` screen "League Leaderboard (Vivid)" (`866d49cb3eb64879beff714b05b53fd5`).
> Current league page has a flat list. Design shows top-3 as large podium cards with avatars and crown/medal icons.

### [ ] Top-3 podium section
- [ ] Replace first 3 rows in league list with large featured cards: avatar, crown/medal rank icon, name, XP
- [ ] 🥇 1st gets crown icon, 🥈 🥉 get medal icons
- [ ] Remaining ranks (4–N) keep standard row layout

### [ ] Current user highlight row
- [ ] User's own row always visible (even if rank > 10), highlighted with "Current Promotion Zone" badge if in top-10
- [ ] Show rank number prominently

### [ ] Motivational banner
- [ ] "KEEP PUSHING!" or similar banner between podium and the rest of the list

---

## ~~Phase 21 — Daily Goal & Streak Widget on Dashboard~~ [SKIP]

> Design source: project `16384358117617625529` screens "Skill Tree Dashboard (Duo-Style)" (`7acba62367db4bdd8e576d3a07353ba3`) and "Дашборд: Дерево навыков" (`1404814a34ac48c49fc0411a00df3d31`).
> Both show a "Daily Progress" panel and a daily XP goal bar on the main skill tree page.

### [ ] Daily XP goal bar on /tree
- [ ] Backend: `GET /profile` adds `dailyXpGoal` (default 50 XP) and `dailyXpToday`
- [ ] Frontend: progress bar "X / Y XP today" shown in StatsWidget or dedicated card on `/tree`
- [ ] Visual: near-complete state (green fill) vs incomplete (gray)

### [ ] Daily streak widget upgrade
- [ ] Current streak card on `/tree` shows only number — add "X days!" label with flame + progress prompt ("One more lesson to keep your streak!")
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
- [ ] `QuestSeeder` — seeds 3 daily quests refreshed each day (e.g., "Complete 3 exercises", "Earn 100 XP", "Log in 3 days in a row")
- [ ] `GET /quests` — returns active quests with user progress
- [ ] `QuestProgressJob` — evaluates quest progress after exercise submit

### [ ] Frontend — /quests page
- [ ] Quest cards: title, description, progress bar (X/Y), XP reward badge
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

## ~~Phase 26 — Performance: Redis Leaderboard~~ [SKIP]

> Deferred from Phase 7.

### [ ] Redis leaderboard sorted set
- [ ] Replace DB query for league rankings with Redis sorted set (`ZADD`, `ZRANK`, `ZRANGE`)
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
- [x] `POST /dialog/sessions/{sessionId}/complete` — end session, generate feedback, award XP
- [x] Return 503 if OpenAI not configured

### [x] Backend — Admin dialog endpoints
- [x] `AdminDialogController` with RequireAdmin policy
- [x] `GET/POST/PUT/DELETE /admin/dialog/bundles` (with skillId)
- [x] `GET/POST /admin/dialog/bundles/{bundleId}/modes` (with prompts)
- [x] `PUT/DELETE /admin/dialog/modes/{id}` (edit prompts)

### [x] Backend — Seed test data
- [x] `DialogSeeder` — seed "Холодные звонки" bundle (linked to skill) + "Обход секретаря" mode
- [x] Run seeder on startup (idempotent)

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
- [x] `SessionHistorySidebar` — sessions grouped by date, XP badges
- [x] `/dialog/[bundleId]/[modeId]/page.tsx` — full-screen chat with sidebar
- [x] Toggle sidebar, load previous sessions
- [x] "Новый диалог" button starts fresh session

### [x] Frontend — Session completion & feedback
- [x] Detect `isStopSignal` from AI response
- [x] Show "Завершить диалог" button when stop detected
- [x] On complete → call `/sessions/{id}/complete` → show `FeedbackModal`
- [x] FeedbackModal: XP badge, feedback text, "Новый диалог" button

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
- [ ] Unit tests for `OpenAiChatService` (mocked HTTP)
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
- [ ] Frontend component tests for VoiceMicButton

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
- [ ] Unit tests for non-AI evaluation strategies
- [ ] Unit tests for AI strategies (mocked HTTP)
- [ ] Integration tests for chat endpoint