# AI_SERVICE.md — AI Engine Service extraction

> Phase 6 of the [microservices migration](MICROSERVICES_ROADMAP.md). Extracts all
> LLM/speech compute out of the monolith (`src/backend/api`) into an independently
> deployable `ai-service`. The monolith slices are left in place as reference; the
> gateway flips the relevant routes to the new service (strangler fig).

## Bounded context

Everything that talks to an LLM or a speech API:

- **Dialog** — GPT roleplay (chat + AI feedback scoring).
- **Voice** — streaming TTS (Yandex SpeechKit / Google Cloud TTS) over the roleplay.
- **Transcription** — Whisper speech-to-text.
- **Evaluation** — the AI grading strategies pulled out of the monolith's `Exercises`
  slice, exposed as a synchronous `POST /ai/evaluate` endpoint for the Learning service.

## Layout

```
src/backend/ai-service/
  Ai/
    Program.cs                         service host wiring
    Sellevate.Ai.csproj
    Dockerfile                         build context = src/backend (for building-blocks)
    Common/Constants/
    Eventing/                          dialog.evaluated publisher + consumers + weights cache
    Features/
      Dialog/                          bundles/modes CRUD, sessions, chat, feedback
      Voice/                           TTS router (+cache), voice streaming, usage limits
      Transcription/                   Whisper STT
      Evaluation/                      POST /ai/evaluate + the 5 AI grading strategies
    Infrastructure/
      Configuration/                   OpenAI / Whisper / Yandex / Google / Voice options
      Data/                            AiDbContext (Postgres) + EF migrations
      Http/                            upstream connection warmup
      Mongo/                           MongoDbContext (dialog_sessions)
  Ai.Tests/                            NUnit unit tests
```

## Data ownership

| Store | Owns | Notes |
|---|---|---|
| Postgres `ai` | `DialogBundles`, `DialogModes` | Roleplay catalog config. `SkillId` is a loose `Guid` (Skills are owned by Learning — no cross-DB FK). |
| Postgres `ai` | `UserReplicas` | Local read-model (`UserId`, `Email`, `DisplayName`, `AvatarKey`) fed by `user.*` Kafka events. Used by the admin voice-usage report instead of joining Identity. |
| Mongo `sallevate` | `dialog_sessions` | Roleplay transcripts + per-session voice seconds. |
| Redis | TTS audio cache (in-process) + Kafka idempotency store | |

`DatabaseBootstrapper` creates the `ai` database on startup, then EF migrations run
(`InitialAiSchema`).

## Coupling broken during extraction

| Monolith coupling | Resolution in ai-service |
|---|---|
| `DialogService` → `IGamificationService.GetSettingsAsync()` for XP weights | Weights are cached locally in `IDialogScoringWeightsProvider` (default 25/25/25/25 ×1.0), refreshed from the `gamification.dialog-weights.updated` Kafka event. No live cross-call. |
| `DialogController` writing `UserXp` rows directly | On session completion the service emits `dialog.evaluated`; Gamification grants the XP. |
| Evaluation strategies reading `ExerciseTypePrompt` from the monolith DB | The system prompt text is passed **into** `POST /ai/evaluate` by the caller (Learning owns `ExerciseTypePrompt`); the AI service performs only the LLM call. |
| `DialogBundle.Skill` navigation / `Skills` table reads in admin CRUD + import | Dropped. Admin create/update/import take `skillId` (a `Guid`) directly. |
| `MongoDbContext` exposing `chat_conversations` (Social) | Removed — AI owns only `dialog_sessions`. |

## Kafka

- **Produces:** `dialog.evaluated` (`userId`, `sessionId`, `bundleId`, `modeId`,
  `rawScore`, `xpEarned`), partition key = `userId`.
- **Consumes:** `gamification.dialog-weights.updated` (refresh scoring weights cache),
  and `user.registered` / `user.updated` / `user.deleted` (maintain the `UserReplica`).
  Both consumers are idempotent (dedupe on `eventId` via the shared Redis store).

## Routes (through the gateway, paths preserved)

Flipped to the `ai` cluster: `/dialog/*` (incl. `/dialog/voice/*` and
`/dialog/sessions/{id}/voice/stream`), `/transcription/*`, `/admin/dialog/*`,
`/admin/voice/*`. `IAsyncEnumerable` voice streaming is preserved end-to-end.

`POST /ai/evaluate` is an **internal** service-to-service endpoint (Learning → AI on
the docker network); it is intentionally **not** exposed through the gateway.

## Running locally

Infra (`scripts/dev-infra.sh`) then `scripts/dev-ai.sh` (host, port 5003), or the full
Docker stack `docker compose up --build -d ai gateway`. Health: `GET /healthz`.

See [docs/TESTING/AI_SERVICE.md](TESTING/AI_SERVICE.md) for the test layout and the
manual checklist. The original feature specs remain at [AI_DIALOG.md](AI_DIALOG.md)
and [VOICE_ROLEPLAY.md](VOICE_ROLEPLAY.md).
