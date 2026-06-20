# DATA_OWNERSHIP.md — Monolith `AppDbContext` → Service Ownership Matrix

> Phase 0.7 deliverable. Maps **every** entity in the monolith's single
> `AppDbContext` to the microservice that will own it (per
> [MICROSERVICES.md](MICROSERVICES.md) §2 / §5), and lists the cross-feature
> references that must be broken during extraction.
>
> **No code is moved by this document** — it is the ownership contract the later
> extraction phases (2–8) follow. The monolith keeps all 42 entities until each
> service's routes are flipped at the gateway (strangler fig).

The monolith currently has **42 entities** in one `AppDbContext`. Target: each one
lives in exactly one service's own database, with the shared `User` replaced by a
local `UserReplica` (see [BuildingBlocks](../src/backend/building-blocks)) everywhere
it used to be foreign-keyed.

## Ownership matrix

| Entity (`DbSet`) | Owning service | Store | Notes |
|---|---|---|---|
| `User` | **Identity** | identity-db (PG) | Source of truth; JWT subject. Replicated elsewhere as `UserReplica`. |
| `RefreshToken` | **Identity** | identity-db (PG) | |
| `EmailVerificationCode` | **Identity** | identity-db (PG) | |
| `UserProfile` | **Identity** | identity-db (PG) | Written by Onboarding + Profile (both inside Identity). |
| `DefaultAvatar` | **Identity** | identity-db (PG) | Avatar blobs in S3/MinIO. |
| `Skill` | **Learning** | learning-db (PG) | |
| `SkillStage` | **Learning** | learning-db (PG) | |
| `Topic` | **Learning** | learning-db (PG) | |
| `Lesson` | **Learning** | learning-db (PG) | |
| `Exercise` | **Learning** | learning-db (PG) | |
| `ExerciseTypePrompt` | **Learning** | learning-db (PG) | Global AI prompt per exercise type (prompt text; AI call lives in AI service). |
| `OpenQuestionGlobalContext` | **Learning** | learning-db (PG) | Shared context for open-question grading. |
| `ReferenceMaterial` | **Learning** | learning-db (PG) | |
| `Technique` | **Learning** | learning-db (PG) | |
| `TechniqueSkill` | **Learning** | learning-db (PG) | |
| `TechniqueCoach` | **Learning** | learning-db (PG) | |
| `DailyQuote` | **Learning** | learning-db (PG) | |
| `UserSkillProgress` | **Learning** | learning-db (PG) | Progress is content-local; keyed by `UserId` (replica). |
| `UserLessonProgress` | **Learning** | learning-db (PG) | Was written by Lessons + Exercises → single owner. |
| `UserExerciseAttempt` | **Learning** | learning-db (PG) | AI-graded types call AI `POST /ai/evaluate` synchronously. |
| `UserTechniqueProgress` | **Learning** | learning-db (PG) | Emits `technique.mastery.changed`. |
| `DialogBundle` | **AI Engine** | ai-db / config | Roleplay bundle config. |
| `DialogMode` | **AI Engine** | ai-db / config | Chat + feedback system prompts, voice flags. |
| `UserXp` (`UserXpRecords`) | **Gamification** | gamification-db (PG) | Written only by Gamification reacting to `exercise.completed` / `dialog.evaluated`. |
| `UserStreak` | **Gamification** | gamification-db (PG) | Single-writer (was multi-writer in monolith). |
| `GamificationSettings` | **Gamification** | gamification-db (PG) | |
| `ExerciseTypeReward` | **Gamification** | gamification-db (PG) | XP economy config. |
| `StreakMilestone` | **Gamification** | gamification-db (PG) | |
| `Achievement` | **Gamification** | gamification-db (PG) | |
| `UserAchievement` | **Gamification** | gamification-db (PG) | Emits `achievement.unlocked`. |
| `League` | **Gamification** | gamification-db (PG) | League is inside Gamification — reads XP from same DB. |
| `LeagueTier` | **Gamification** | gamification-db (PG) | |
| `LeagueMembership` | **Gamification** | gamification-db (PG) | |
| `LeagueSettings` | **Gamification** | gamification-db (PG) | |
| `Friendship` | **Social** | social-db (PG) | Emits `friend.request.received/accepted`. |
| `DiscussThread` | **Social** | social-db (PG) | |
| `DiscussReply` | **Social** | social-db (PG) | |
| `DiscussVote` | **Social** | social-db (PG) | |
| `DiscussTag` | **Social** | social-db (PG) | |
| `DiscussThreadTag` | **Social** | social-db (PG) | Join table. |
| `DiscussPhoto` | **Social** | social-db (PG) | Photo blobs in S3/MinIO. |
| `Notification` | **Notifications** | Redis | Relational table replaced by a capped per-user Redis list + 30-day TTL. |

**Mongo collections (not in `AppDbContext`):** `dialog_sessions` / `chat_messages`
→ **AI Engine** (`ai-db`); `chat_conversations` → **Social** (`social-mongo`).

**Redis-only services:** Notifications (inbox) and Analytics (presence/counters) —
no relational store at all.

## Cross-feature references to break

These are the joins / direct entity reads that cross a future service boundary and
must be replaced (by a `UserReplica` read or a Kafka event) before extraction:

| Cross-reference in the monolith | Breaks boundary | Resolution |
|---|---|---|
| Everything joining `User` for name/avatar | every service ↔ Identity | Local `UserReplica`, synced via `user.*` events. |
| `Exercises` / `Dialog` writing `UserXp` | Learning/AI ↔ Gamification | Gamification writes XP, reacting to `exercise.completed` / `dialog.evaluated`. |
| `Exercises` updating `UserStreak` | Learning ↔ Gamification | Gamification owns streaks (single writer). |
| `League` reading `UserXp` | (same service) | League stays **inside** Gamification — no cross-service read. |
| `Exercises` AI grading strategies calling OpenAI | Learning ↔ AI | Learning calls AI `POST /ai/evaluate` (sync). |
| `Dialog` reading Gamification XP weights | AI ↔ Gamification | AI caches weights from `gamification.dialog-weights.updated`. |
| `Lessons` + `Exercises` both writing `UserLessonProgress` | (same service) | Both live in Learning — single owner. |
| `Onboarding` + `Profile` both writing `UserProfile` | (same service) | Both live in Identity — Onboarding becomes an internal flow. |
| `Notifications` triggered by Achievements/Streak/Friends/Chat | many ↔ Notifications | Notifications consumes events instead of being called in-process. |

See [MICROSERVICES.md §4.1](MICROSERVICES.md) for the full Kafka event catalogue
and [MICROSERVICES_ROADMAP.md](MICROSERVICES_ROADMAP.md) for the extraction order.
