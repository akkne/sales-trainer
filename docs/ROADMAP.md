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

### [ ] League promotion/demotion logic
- [ ] `WeeklyLeagueClosureJob` — already scheduled, implement full logic:
  - Top-10 in each league promoted to next tier
  - Bottom-5 demoted
  - New week: reset `weekly_xp`, create new `LeagueMembership` rows
- [ ] Frontend league page shows promotion/demotion zone highlights
- [ ] Push notification / banner on week close (in-app only)

---

## Phase 8 — Duolingo Green Redesign (Lesson Execution)

> Full visual redesign of the main app flow based on Stitch design `projects/5546133593140033209`.
> Spec: [docs/LESSON_EXECUTION_REDESIGN.md](LESSON_EXECUTION_REDESIGN.md)

### [ ] Design tokens & fonts
- [ ] Add Manrope font via `next/font/google` in `app/layout.tsx`
- [ ] Update `globals.css`: body font → Manrope, define CSS color variables for green scheme
- [ ] Add CSS utility classes: `node-center`, `node-left`, `node-right`, `btn-3d`, `slide-up`

### [ ] SkillNode component redesign
- [ ] `variant: 'completed' | 'active' | 'locked'` prop
- [ ] Completed: yellow circle `#FFC800`, gold medal badge top-right
- [ ] Active: green circle `#59c705`, `animate-ping` outer ring, default-open popover card above node
- [ ] Locked: gray `#F7F7F7` circle, lock icon, `cursor-not-allowed`
- [ ] Zigzag offset via `node-left` / `node-right` / `node-center` CSS classes
- [ ] Popover card: lesson title, mini progress bar (X/total), "Старт" green button

### [ ] StatsWidget redesign
- [ ] 3 separate border cards: 🔥 Streak (yellow), ⚡ XP (blue), 🏆 League (red/shield)
- [ ] Each card: `border-2 border-border-color rounded-2xl`, hover accent border
- [ ] Mascot card below with motivational tip text

### [ ] Skill Tree page (`/tree`) redesign
- [ ] Section header banner: `bg-primary text-white rounded-[24px] shadow-[0_4px_0_0_#58A700]`
- [ ] Section header shows: name, description, `X/total` progress fraction badge
- [ ] Locked section: `bg-surface border-2` with lock icon
- [ ] Vertical path line: absolute div `left-1/2 -translate-x-1/2 w-4 bg-border-color`
- [ ] Active segment of path line: `bg-primary` overlay at proportional height
- [ ] Boss/chest node at end of each section (rotated square icon)
- [ ] Right sidebar: new StatsWidget + mascot

### [ ] Skill Path page (`/skill/[id]`) redesign
- [ ] Replace lesson list with vertical node path
- [ ] SVG overlay with curved `C` bezier connectors between nodes (dashed animated on active)
- [ ] Lesson nodes: same completed/active/locked variants as SkillNode
- [ ] Alternating left/right horizontal offsets
- [ ] Active node popover: lesson name, "Урок X из N", green "Старт" button
- [ ] Right sidebar: stats mini bar + skill description + progress bar + guidebook link

### [ ] Exercise page (`/exercise/[id]`) redesign
- [ ] New header: X button + green progress bar + ❤️ hearts counter (visual, starts at 4)
- [ ] Character speech bubble for situation field (portrait + bubble with arrow)
- [ ] Multiple choice: numbered badge (1/2/3) + `border-b-4` 3D shadow buttons
- [ ] Selected state: `border-accent-blue bg-[#e8f7fe] text-accent-blue`
- [ ] Replace `ExerciseResultBanner` with slide-up green/red panels:
  - Correct: `bg-[#d7ffb8]`, checkmark icon, explanation, "ПРОДОЛЖИТЬ" green button
  - Incorrect: `bg-[#ffdfe0]`, cancel icon, correct answer shown, "ПОНЯТНО" red button
- [ ] Apply same speech bubble + numbered options to FillBlankExercise
- [ ] Keep FreeTextExercise layout, apply 3D button style to submit/mic buttons

### [ ] Smoke test
- [ ] Full lesson flow: tree → skill path → exercise → result banner → next exercise
- [ ] Verify all node states render correctly
- [ ] Verify correct/incorrect banner slides up and continues properly

---

## Phase 7 — Polish & Mobile

### [ ] Mobile UX pass
- [ ] Responsive skill tree (touch-friendly nodes)
- [ ] Exercise screen bottom-safe-area padding
- [ ] Profile page on small screens
  
### [ ] Performance
- [ ] Redis caching for skill tree (`GET /skill-tree`) — 60s TTL, invalidate on admin skill change
- [ ] Redis leaderboard sorted set for league rankings