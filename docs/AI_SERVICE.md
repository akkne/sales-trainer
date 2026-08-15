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
| Postgres `ai` | `DialogBundles`, `DialogModes` | Roleplay catalog config. `SkillId` is a loose `Guid` (Skills are owned by Learning — no cross-DB FK). Tenancy: `OrganizationId` nullable — `NULL` is the global library, non-null is org-authored — with an EF query filter and RLS on both tables (40.11). |
| Postgres `ai` | `UserReplicas` | Local read-model (`UserId`, `Email`, `DisplayName`, `AvatarKey`) fed by `user.*` Kafka events. Used by the admin voice-usage report instead of joining Identity. **No `OrganizationId`**: it is a consumer-fed projection with no request and therefore no tenant, the same call learning-db made in 40.10. |
| Mongo `sallevate` | `dialog_sessions` | Roleplay transcripts + per-session voice seconds. Tenancy: `organizationId` on every document, enforced by `DialogSessionRepository` alone — Mongo has no RLS (40.11). |
| Redis | scenario-verdict cache + voice quota counters + Kafka idempotency store; TTS audio cache is in-process | Every ai-service key is namespaced `org:{organizationId}:` (40.11). |

`DatabaseBootstrapper` creates the `ai` database on startup, then EF migrations run
(`InitialAiSchema` … `AddOrganizationId`). Index rebuilds and the Mongo backfill are **not** part
of startup — they are operational steps, driven by
`scripts/tenancy-ai-organization-rollout.sh`.

### Multi-tenancy (Phase 40.11)

ai-service is the first service whose tenant boundary spans three stores, and each of them fails
differently:

| Store | What enforces the boundary | What happens with no tenant on the request |
|---|---|---|
| Postgres | RLS policy (`EnableTenantRlsForContent`) + EF query filter as convenience | The policy admits global rows only |
| Mongo | `DialogSessionRepository`, and nothing else — there is no database-side net | Raises `InvalidOperationException`; never "all sessions" |
| Redis | The key name (`org:{organizationId}:…`) | Verdict cache is skipped; quota keys raise |

The one behavioural change worth knowing: the SuperAdmin voice-usage report aggregates the
caller's organization instead of the whole installation. A cross-tenant total is exactly the leak
40.11 closes; a platform superadmin reaches another organization's numbers by impersonating into
it (40.9), and the org-scoped admin surface arrives in 40.20.

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
  Both consumers are idempotent (dedupe on `eventId` via the shared Redis store, keyed
  `org:{organizationId}:idem:{group}:{eventId}` since 40.11 — the organization comes from the
  envelope, and an event without one keeps the historical un-prefixed key).

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
