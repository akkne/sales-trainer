# Features & Architecture Docs

All significant features, architectural decisions, and infrastructure docs.

## Core Documentation

| Document | Description |
|----------|-------------|
| [Architecture](ARCHITECTURE.md) | Stack overview, folder structure, EF column types |
| [Microservices (target)](MICROSERVICES.md) | Target microservices architecture: 7 services + YARP gateway, DB-per-service, Kafka events, service catalogue & contracts |
| [Microservices Roadmap](MICROSERVICES_ROADMAP.md) | Phased strangler-fig migration of the monolith into microservices, with atomic per-phase tasks |
| [Microservices Review & Remediation](REVIEW_MICROSERVICES.md) | Post-migration code-review findings (7 services + gateway + BuildingBlocks), severity-rated, with remediation status tracker |
| [AI Engine Service](AI_SERVICE.md) | Phase 6: extracted `ai-service` (Dialog, Voice, Transcription, `/ai/evaluate`); Postgres+Mongo, `dialog.evaluated`, cached scoring weights |
| [Gamification Service](GAMIFICATION_SERVICE.md) | Phase 7: extracted event-driven `gamification-service` (XP, streaks, achievements, league) on Postgres `gamification`; consumes `exercise.completed`/`dialog.evaluated`/`lesson.completed`/`skill.completed`, produces `xp.granted`/`achievement.unlocked`/`streak.milestone`/`gamification.dialog-weights.updated`; Hangfire streak-reset + weekly-league-closure jobs; `/gamification/*`, `/league/*`, `/profile/achievements`, `/admin/gamification/*`, `/admin/leagues/*` flipped at the gateway |
| [Analytics Service](ANALYTICS_SERVICE.md) | Phase 1: extracted Redis-only `analytics-service` (tracking, presence, funnels); `/tracking/*` flipped at the gateway; consumes `user.registered`/`exercise.completed`/`xp.granted`; owns the product Prometheus metrics |
| [Notification Service](NOTIFICATION_SERVICE.md) | Phase 4: extracted `notification-service` (Redis-only); consumes 5 social/gamification events, per-user capped inbox + unread counter with 30-day TTL (replaces Hangfire cleanup job) |
| [Data Ownership Matrix](DATA_OWNERSHIP.md) | Phase 0.7: every `AppDbContext` entity → owning service, plus cross-feature references to break |
| [Identity Service](IDENTITY_SERVICE.md) | Phase 2: extracted identity microservice — sole JWT issuer, own `identity-db`, `user.*` Kafka events, gateway route flip |
| [Social Service](SOCIAL_SERVICE.md) | Phase 5: extracted `social-service` (Friends, Discuss forum, Chat); Postgres `social` + shared Mongo `chat_conversations` + MinIO photos; produces `friend.request.received`/`friend.request.accepted`/`chat.message.sent`; `/friends/*`, `/discuss/*`, `/admin/discuss/*`, `/chat/*` flipped at the gateway |
| [Learning Service](LEARNING_SERVICE.md) | Phase 8: extracted `learning-service` (SkillTree, Lessons, Exercises, Reference, Techniques, DailyQuotes + content admin/seeder) on Postgres `learning`; deterministic grading local, AI types call ai-service `/ai/evaluate`; produces `exercise.completed`/`lesson.completed`/`skill.completed`/`technique.mastery.changed`; `/skills/*`, `/skill-tree`, `/lessons/*`, `/topics/*`, `/exercises/*`, `/reference/*`, `/techniques/*`, `/daily-quote` + learning `/admin/*` flipped at the gateway (monolith now serves no routes) |
| [API Contracts](API_CONTRACTS.md) | All REST endpoints with request/response schemas |
| [DB Schema](DB_SCHEMA.md) | PostgreSQL tables, MongoDB collections, Redis keys |
| [Decisions](DECISIONS.md) | Non-trivial engineering decisions with alternatives and rationale |
| [Code Style](CODESTYLE.md) | Naming, file structure, patterns, DI rules |
| [Codestyle Enforcement](CODESTYLE_ENFORCEMENT.md) | PR CI gate for CODESTYLE.md — custom linter (no comments, no abbreviations) + `dotnet format` + `.editorconfig` |
| [Task Workflow](TASK_WORKFLOW.md) | Board-driven PLAN→STOP→EXECUTE→VERIFY pipeline (OMC agents) — `/run-task` command + `run-tasks-poll` automation |
| [Local Dev](LOCAL_DEV.md) | Run backend/frontend on the host (no image rebuilds) with infra in Docker — `scripts/dev-*.sh`, `docker-compose.infra.yml` |
| [Configuration](CONFIGURATION.md) | Secrets in root .env, per-service config files, env var → appsettings mapping |
| [Deployment](DEPLOYMENT.md) | Production deploy: frontend on Vercel (`sellevate.vercel.app`), backend + infra via Docker Compose, CORS allow-list, env vars, optional Kubernetes/Helm (Option C) + health probes |
| [Production Migration (monolith → microservices)](MICROSERVICES_PRODUCTION_MIGRATION.md) | Single-server cutover runbook: resource/cost impact (RAM/CPU/disk, no GPU), backup, DB-per-service split via `scripts/migrate-monolith-to-services.sh`, cutover order, rollback, `.env` tunables |
| [Integrations](INTEGRATIONS.md) | External service integrations: MinIO/S3 object storage, endpoints, env keys |
| [Monitoring & Product Metrics](MONITORING.md) | Usage metrics on Prometheus/Grafana: online users, visits/day/week, page views, UI events, logins/registrations — catalog, cardinality rules, dashboard |
| [Seeder](SEEDER.md) | CSV/JSON import format for skills and lessons |
| [Admin Panel](ADMIN_PANEL.md) | Roles, authorization, CRUD endpoints, UI structure |
| [Redesign Prompt](REDESIGN_PROMPT.md) | Ready-to-paste Claude Design / Stitch brief for the full UI redesign |
| [UI Current State](UI_CURRENT_STATE.md) | Exhaustive baseline snapshot of all screens, elements, and visual tokens for the redesign |
| [Handbook Redesign](HANDBOOK_REDESIGN.md) | Implemented 2026-04-21 — Technique domain with per-user mastery + structured dialog/case/coach blocks |
| [Mobile Responsive Testing](TESTING/MOBILE_RESPONSIVE.md) | Breakpoints + manual phone checklist for the full mobile adaptation (user-facing screens + admin drawer) |
| [Redesign Roadmap](REDESIGN_ROADMAP.md) | New design system rollout (electric blue/violet, Manrope/Unbounded) — phase status and verification notes |

## Feature Documentation

| Feature | Description |
|---------|-------------|
| [Skills & Exercises](SKILLS_AND_EXERCISES.md) | Skill/lesson/exercise data model, evaluation logic |
| [New Exercise Types](NEW_EXERCISE_TYPES.md) | 11 exercise types: 5 basic + 5 AI-evaluated + theory cards |
| [AI Dialog](AI_DIALOG.md) | GPT-powered sales conversation practice |
| [Voice Roleplay](VOICE_ROLEPLAY.md) | Voice-based practice with VAD, Deepgram STT, ElevenLabs TTS |
| [Friends & Chat](FRIENDS.md) | Friendships, public profiles, user search, leaderboard, 1-to-1 chat |
| [Notifications](NOTIFICATIONS.md) | In-app notification bell, social and gamification triggers, 30-day cleanup |
| [Email Notifications](EMAIL_NOTIFICATIONS.md) | Opt-in email channel: unread direct message (delayed 5 min), discuss reply, league update; OOP HTML templates in notification-service; shared MailerSend transport in BuildingBlocks |
| [Discuss](DISCUSS.md) | Community forum: threads, replies, upvotes, hybrid tags, solved/hot, admin moderation |
| [Email Verification](EMAIL_VERIFICATION.md) | Registration confirmed by an emailed numeric code (MailerSend); login gated on a verified address |
| [Seeder](SEEDER.md) | Bulk import content: skills, topics, lessons with exercises via JSON |

## Testing

All test documentation is in the [TESTING/](TESTING/) folder:

| Document | Scope |
|----------|-------|
| [CORE.md](TESTING/CORE.md) | Test strategy, tooling, how to run |
| [EMAIL_VERIFICATION.md](TESTING/EMAIL_VERIFICATION.md) | Registration code flow: unit, integration, manual checklist |
| [BACKEND_UNIT.md](TESTING/BACKEND_UNIT.md) | Unit test roadmap |
| [BACKEND_INTEGRATION.md](TESTING/BACKEND_INTEGRATION.md) | Integration test roadmap |
| [FRONTEND.md](TESTING/FRONTEND.md) | Vitest setup, component tests |
| [EXERCISE_CONTENT_VALIDATION.md](TESTING/EXERCISE_CONTENT_VALIDATION.md) | Per-type content validation: unit tests, integration tests, frontend type checking |
| [HEADER_PROFILE_BUTTON.md](TESTING/HEADER_PROFILE_BUTTON.md) | Desktop header profile chip and achievement button cleanup |
| [VOICE_CALL.md](TESTING/VOICE_CALL.md) | Telephone call mode: connect, barge-in, hangup, minute limits |
| [NIGHT_POLISH.md](TESTING/NIGHT_POLISH.md) | Phase 37: April palette purge, call sounds/vibration/barge-in, voice usage report, skeletons & error states |
| [DISCUSS.md](TESTING/DISCUSS.md) | Community forum: threads, replies, voting, tags, accepted answer, admin moderation |
| [DISCUSS_PHOTOS.md](TESTING/DISCUSS_PHOTOS.md) | Discuss photo attachments: upload, max-count, auth, magic-byte validation, cascade delete, PhotoPicker component |
| [USER_AVATARS.md](TESTING/USER_AVATARS.md) | User avatar upload on own profile: hover overlay, file picker, cache-busting, fallback |
| [MICROSERVICES_FOUNDATIONS.md](TESTING/MICROSERVICES_FOUNDATIONS.md) | Phase 0: building-blocks (envelope, idempotency, identity headers) + YARP gateway passthrough/anti-spoof tests |
| [ANALYTICS_SERVICE.md](TESTING/ANALYTICS_SERVICE.md) | Phase 1: presence window math, usage-event counters, funnel event consumption, gateway route-flip config |
| [NOTIFICATION_SERVICE.md](TESTING/NOTIFICATION_SERVICE.md) | Phase 4: notification-service unit tests (event→inbox, unread count, mark-read, capping, TTL, mapper) + gateway route-flip |
| [GAMIFICATION_SERVICE.md](TESTING/GAMIFICATION_SERVICE.md) | Phase 7: gamification-service unit tests (XP grant per event type, streak increment/reset/milestone, achievement unlock + idempotency, league rollover, outgoing event contracts) + gateway route-flip |
| [IDENTITY_SERVICE.md](TESTING/IDENTITY_SERVICE.md) | Phase 2: identity microservice — auth flow, onboarding/profile, avatar + `user.*` event unit/integration tests |
| [SOCIAL_SERVICE.md](TESTING/SOCIAL_SERVICE.md) | Phase 5: social-service unit tests (friend lifecycle + events, forum CRUD/voting/photos, chat friendship guard, `user.*` replica consumer, event contract) + gateway route-flip |
| [LEARNING_SERVICE.md](TESTING/LEARNING_SERVICE.md) | Phase 8: learning-service unit tests (deterministic grading, AI grading via mocked `/ai/evaluate`, submit event emission, skill-tree progress, technique progress, outgoing event contracts) + gateway route-flip |
| [HARDENING.md](TESTING/HARDENING.md) | Phase 10: health-check response shape + gateway liveness, dead-letter/retry policy (`EventMessageProcessor`), and cross-service Kafka schema contract catalogue |
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
- Skills grouped on `/tree` by DB-driven funnel stages (label/accent/order), editable via `/admin/skill-stages` — full CRUD, no hardcoded list ([API_CONTRACTS](API_CONTRACTS.md), [DB_SCHEMA](DB_SCHEMA.md))

### Exercise Session
- Full-screen session with progress bar
- 11 exercise types: multiple_choice, fill_blank, free_text, ordering, matching, categorizing, find_error, rewrite_better, ai_dialog, rate_call, written_answer
- AI evaluation for free-text and complex types (passing threshold: score >= 7/10)
- Exercise retry queue: failed exercises are queued at end of lesson (max 2 attempts per exercise)
- Keyboard shortcuts (1-4 select, Enter submit)
- Skip button, post-session stats (XP, accuracy, time)
- Completion screen with session summary
- **Theory lessons** (`theory_card` type): stories-style cards (text / dialogue / bullets / quote)
  the learner swipes through before practice — no answer, no AI. Dialogue cards reuse the
  Guidebook bubble renderer. Marked with a book icon on the path; reaching the last card
  completes the lesson and awards a small fixed XP (seeded 5, admin-editable)

### Gamification
- **Fully DB-driven, admin-editable XP economy** (no hardcoded constants) — see `GamificationSettings`, `ExerciseTypeRewards`, `StreakMilestones` in [DB_SCHEMA](DB_SCHEMA.md):
  - Per-exercise-type base XP (edited at `/admin/gamification/exercise-rewards`)
  - Dialog XP = `round(AI score × multiplier)` with admin-tunable multiplier + per-criterion weights (edited at `/admin/dialog`)
  - Daily & weekly XP goals (edited at `/admin/gamification`)
  - Streak milestone bonuses as a CRUD ladder (edited at `/admin/gamification`)
- Daily streak tracking with reset job
- Achievement system with 10 default achievements
- Achievement unlock toasts during session

### Leagues
- Period leaderboards over a configurable tier ladder (default Bronze → Silver → Gold → Diamond)
- Editable tiers (key/name/color/order) via `/admin/leagues/tiers` — full CRUD, no hardcoded list
- Top-N promotion, bottom-M demotion (zone sizes configurable in DB via admin)
- Countdown timer to the exact period end (`periodEndsAt`); the period end date & length are admin-settable for a custom schedule
- Rollover job (every 15 min) closes the period only once its configured end has passed
- Admin management at `/admin/leagues`: browse leagues by period/tier with full history, view members (XP, rank, outcome), move members between tiers, adjust weekly XP (via `admin_correction` XP records), remove members, force XP re-sync, manually close the period, edit settings (zones, period end date, period length), and manage the tier ladder

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
- Daily quote scheduling on a month calendar (`/admin/quotes`) — drives the "Совет дня" widget
- Discuss moderation (`/admin/discuss`): pin/hot/delete threads, delete replies, curated tag CRUD
- User management (`/admin/users`, admins): rich user list (avatar, email + verification, auth provider, role), per-user detail modal with activity stats (XP, streaks, skills, avg score, persona), moderation rename of inappropriate nicknames, and removal of inappropriate uploaded photos (resets to default avatar). Role changes remain SuperAdmin-only.

### User Avatars
- Custom avatar upload on own profile page (`POST /avatars`, multipart, ≤5 MB, png/jpg/webp)
- Camera-emoji 📷 hover overlay on the profile avatar opens a file picker; spinner shown during upload
- Cache-busting `?v=<n>` appended to avatar URL after successful upload so the new image appears immediately
- GeoAvatar (coloured SVG) fallback for users with no uploaded avatar or when the image fails to load
- Public friend profile pages are read-only — no upload affordance shown there

### Usage Metrics / Analytics
- Product metrics on the existing Prometheus + Grafana stack (no new infra)
- Online users (Redis-presence gauge, 5-min window), visits today/this week (derived from counters), page views per page, top UI events, logins per method, registrations
- Backend `ActivityTrackingMiddleware` (visits + presence), `POST /tracking/events` with a server-side event/page whitelist (cardinality guard), counters in `AuthController`
- Frontend best-effort tracking (`shared/analytics`): auto page views + a few key click events
- "Sellevate Product Metrics" Grafana dashboard — see [MONITORING.md](MONITORING.md)

### Discuss (Community Forum)
- Threads with title/body and one or more tags; replies; upvote-only voting on threads and replies
- Author or admin marks an accepted reply → thread shows "Решено"; admin pin/hot flags
- Hybrid tags: admin-curated catalog + user free-form tags created on the fly; dynamic popular-tag counts
- Sort hot (pinned-first, time-decayed) / new / unanswered; text search; tag filter; pagination
- Stats: total threads, total replies, top authors of the week (upvotes received in last 7 days)
- Photo attachments: up to 10 images per thread/reply (PNG/JPEG/WEBP, ≤5 MB), stored in S3/MinIO + `DiscussPhotos`; two-step upload, author-only management, cleaned up on delete
