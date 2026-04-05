# SalesTrainer — ROADMAP

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

### [ ] Backend — Achievement system
- [ ] `Achievement` entity: id, key, title, description, iconUrl, condition (type + threshold)
- [ ] `UserAchievement` entity: userId, achievementId, unlockedAt
- [ ] Achievement evaluation on exercise submit: check milestones (first lesson, 7-day streak, 100 XP, etc.)
- [ ] `GET /profile/achievements` — returns list of all achievements with `unlocked: bool, unlockedAt`
- [ ] Seed default achievement set (5–10 entries)
- [ ] Update API_CONTRACTS.md

### [ ] Frontend — Badges on Profile
- [ ] `useAchievements()` hook — fetches `/profile/achievements`
- [ ] Badges grid section on `/profile` page: locked (grayscale) vs unlocked (color) badges
- [ ] Badge card: icon, title, unlocked date or "locked" label
- [ ] Toast notification when new achievement unlocked during session

---

## Phase 12 — Persona-based Onboarding

> Design source: project `16384358117617625529` screens "Onboarding Step 1: Profile Selection" and
> "Onboarding: Select Profile (Vivid)". Shows avatar cards for sales roles (SDR, AE, Account Manager, etc.).

### [ ] Backend — User persona
- [ ] Add `Persona` enum (SDR / AccountExecutive / AccountManager / Founder / Other) to `User` entity
- [ ] EF migration: add `Persona` column (nullable)
- [ ] Expose `persona` in profile API response
- [ ] `PUT /profile/persona` endpoint

### [ ] Frontend — Persona selection step in onboarding
- [ ] New onboarding step 1: persona picker with avatar cards and role descriptions
- [ ] Each card: role icon/avatar, title (e.g. "SDR"), short description
- [ ] Selected card highlighted with green border; "Продолжить" button activates on selection
- [ ] On submit → `PUT /profile/persona` → advance to existing skill selection step
- [ ] Skip link ("Пропустить") for users who don't identify with a role
- [ ] Persona shown on profile page as a badge/tag

---

## Phase 13 — Lesson Course Map

> Design source: project `16384358117617625529` screen "Lesson Map: Objections" (id `4d961c964dd540f9bfe83133b73d6028`).
> A detailed overview screen per skill showing all lessons as a structured course map.

### [ ] Frontend — Skill course map page `/skill/[id]/map`
- [ ] Header: skill title, icon, total lessons, completion %
- [ ] List of lesson cards: lesson number, title, description excerpt, status (locked/active/completed), XP reward
- [ ] Completed lessons: green check, duration shown
- [ ] Active lesson: highlighted card with "Начать" CTA
- [ ] Locked lessons: dimmed, lock icon, shows what unlocks them
- [ ] "Start" button on active lesson → `/session/[lessonId]`
- [ ] Link from skill node popover: "Посмотреть карту курса"

### [ ] Backend — Lesson descriptions
- [ ] Ensure `Lesson` entity has `description` field (add if missing)
- [ ] Expose `description` and `estimatedMinutes` in lessons API response
- [ ] Migration + seed update

---

## Phase 14 — Sales Handbook: Key Techniques

> Design source: project `16384358117617625529` screens "Sales Handbook (Vivid)" and
> "Sales Handbook: Key Techniques" — structured reference with categories, search, technique cards.

### [ ] Backend — Reference material enhancements
- [ ] Add `category` field to `ReferenceItem` entity (e.g. "objections", "cold-calls", "closing")
- [ ] Add `tags` array field
- [ ] `GET /reference?category=&search=` — filter + search endpoint
- [ ] Migration + seed update for existing reference items

### [ ] Frontend — Handbook page redesign (`/guidebook`)
- [ ] Category tabs / filter chips at top (All, Objections, Cold Calls, Closing, etc.)
- [ ] Search input with debounce — filters cards in real-time
- [ ] Technique cards: title, category badge, short excerpt, expand on tap
- [ ] Expanded card: full markdown content, "Related skill" link
- [ ] Empty state when search yields no results
