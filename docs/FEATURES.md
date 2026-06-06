# Features & Architecture Docs

All significant features, architectural decisions, and infrastructure docs.

## Core Documentation

| Document | Description |
|----------|-------------|
| [Architecture](ARCHITECTURE.md) | Stack overview, folder structure, EF column types |
| [API Contracts](API_CONTRACTS.md) | All REST endpoints with request/response schemas |
| [DB Schema](DB_SCHEMA.md) | PostgreSQL tables, MongoDB collections, Redis keys |
| [Code Style](CODESTYLE.md) | Naming, file structure, patterns, DI rules |
| [Configuration](CONFIGURATION.md) | Secrets in root .env, per-service config files, env var → appsettings mapping |
| [Seeder](SEEDER.md) | CSV/JSON import format for skills and lessons |
| [Admin Panel](ADMIN_PANEL.md) | Roles, authorization, CRUD endpoints, UI structure |
| [Redesign Prompt](REDESIGN_PROMPT.md) | Ready-to-paste Claude Design / Stitch brief for the full UI redesign |
| [UI Current State](UI_CURRENT_STATE.md) | Exhaustive baseline snapshot of all screens, elements, and visual tokens for the redesign |
| [Handbook Redesign](HANDBOOK_REDESIGN.md) | Implemented 2026-04-21 — Technique domain with per-user mastery + structured dialog/case/coach blocks |
| [Redesign Roadmap](REDESIGN_ROADMAP.md) | New design system rollout (electric blue/violet, Manrope/Unbounded) — phase status and verification notes |

## Feature Documentation

| Feature | Description |
|---------|-------------|
| [Skills & Exercises](SKILLS_AND_EXERCISES.md) | Skill/lesson/exercise data model, evaluation logic |
| [AI Dialog](AI_DIALOG.md) | GPT-powered sales conversation practice |
| [Voice Roleplay](VOICE_ROLEPLAY.md) | Voice-based practice with VAD, Deepgram STT, ElevenLabs TTS |
| [New Exercise Types](NEW_EXERCISE_TYPES.md) | 8 additional exercise types with AI evaluation |
| [Friends & Chat](FRIENDS.md) | Friendships, public profiles, user search, leaderboard, 1-to-1 chat |
| [Notifications](NOTIFICATIONS.md) | In-app notification bell, social and gamification triggers, 30-day cleanup |

## Testing

All test documentation is in the [TESTING/](TESTING/) folder:

| Document | Scope |
|----------|-------|
| [CORE.md](TESTING/CORE.md) | Test strategy, tooling, how to run |
| [BACKEND_UNIT.md](TESTING/BACKEND_UNIT.md) | Unit test roadmap |
| [BACKEND_INTEGRATION.md](TESTING/BACKEND_INTEGRATION.md) | Integration test roadmap |
| [FRONTEND.md](TESTING/FRONTEND.md) | Vitest setup, component tests |
| [HEADER_PROFILE_BUTTON.md](TESTING/HEADER_PROFILE_BUTTON.md) | Desktop header profile chip and achievement button cleanup |
| [VOICE_CALL.md](TESTING/VOICE_CALL.md) | Telephone call mode: connect, barge-in, hangup, minute limits |
| [NIGHT_POLISH.md](TESTING/NIGHT_POLISH.md) | Phase 37: April palette purge, call sounds/vibration/barge-in, voice usage report, skeletons & error states |
| Feature checklists | Manual test checklists for each feature |

---

## Implemented Features Summary

### Authentication & Onboarding
- Email + password registration/login
- Google OAuth
- JWT access tokens + refresh token cookies
- 4-step onboarding: persona → sales type → experience → skill selection
- Demo mode (no registration)

### Skill Tree & Lessons
- Skill enrollment system (subscribe/unsubscribe)
- Sequential lesson unlock within skills
- Lazy seeding of lesson progress on first access
- Course map view with progress tracking

### Exercise Session
- Full-screen session with progress bar
- 11 exercise types: multiple_choice, fill_blank, free_text, ordering, matching, categorizing, find_error, rewrite_better, ai_dialog, rate_call, written_answer
- AI evaluation for free-text and complex types (passing threshold: score >= 7/10)
- Exercise retry queue: failed exercises are queued at end of lesson (max 2 attempts per exercise)
- Keyboard shortcuts (1-4 select, Enter submit)
- Skip button, post-session stats (XP, accuracy, time)
- Completion screen with session summary

### Gamification
- XP rewards for exercises and dialogs
- Daily streak tracking with reset job
- Achievement system with 10 default achievements
- Achievement unlock toasts during session

### Leagues
- Weekly leaderboards (Bronze → Silver → Gold → Diamond)
- Top-10 promotion, bottom-5 demotion
- Countdown timer to week end
- Weekly closure job

### Reference & Handbook
- **Techniques** ("Коллекция") — first-class entities with per-user mastery ring (Unseen/Novice/Practitioner/Expert/Master), category + tag filtering, sample dialog with annotations, case study, and optional coach sidecar
- Legacy **ReferenceMaterials** markdown glossary kept for skill-detail pages (`GET /skills/:slug/reference`)
- Admin CRUD under `/admin/techniques*`

### AI Dialog
- Bundle/mode structure linked to skills
- Multi-turn conversations with GPT-4.1-mini
- Feedback generation with GPT-4.1
- XP rewards based on AI evaluation
- Session history sidebar

### Voice Roleplay
- Voice Activity Detection (@ricky0123/vad-web)
- Deepgram Nova-3 streaming STT
- ElevenLabs Flash v2.5 TTS
- Target latency: ≤700ms end-to-end

### Friends & Chat
- Friend request system (send, accept, decline, remove)
- Public profiles with stats and friendship status
- User search by display name and email
- Friend XP leaderboard
- Friend activity feed (achievements, XP earned)
- 1-to-1 chat between friends (MongoDB, 5s polling)
- Conversations list with last message preview
- Navigation badge for pending friend requests

### Notifications
- In-app bell with unread badge in top app bar
- Dropdown panel with recent notifications and "Mark all as read"
- Triggers: friend request received/accepted, chat message received, achievement unlocked, streak milestone
- 20s unread count polling, 30s list polling
- Deep-links via actionUrl on notification activation
- Hangfire daily cleanup job deletes read notifications older than 30 days

### Admin Panel
- Role system: User / Admin / SuperAdmin
- Full CRUD for skills, lessons, exercises, reference
- Visual exercise editor for all 11 types
- JSON import with inline editor
- Dialog bundle/mode management with prompt editors
- User role management (SuperAdmin only)
