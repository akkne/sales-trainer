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
| Postgres `ai` | `DialogBundles`, `DialogModes` | Roleplay catalog config. `SkillId` is a loose `Guid` (Skills are owned by Learning — no cross-DB FK). Tenancy: `OrganizationId` nullable — `NULL` is the global library, non-null is org-authored — with an EF query filter and RLS on both tables (40.11). `DialogModes` also carries `ParentModeId` / `BaseContentHash` for copy-on-write prompt overrides (40.18). |
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

### Prompt overrides (Phase 40.18)

An organization customizes a global prompt without forking the prompt library
([TENANCY/CONTENT_MODEL.md](TENANCY/CONTENT_MODEL.md) §2.6, §4). 40.11 had already made `DialogModes`
a content table; this adds the two columns copy-on-write needs (`ParentModeId`, `BaseContentHash`),
the `CK_DialogModes_OverrideHasOwner` constraint, and `AdminDialogOverridesController`.

- **The override keeps its parent's `BundleId` and `Key`.** The 40.11 unique indexes already permit
  it, because the composite one is filtered to non-global rows, and it is what makes the customized
  prompt appear in the same bundle in the same position with no second resolution layer.
- **`DialogBundle` is deliberately not override-able.** It carries no prompt at all — title,
  description, emoji, sort order — and a copied bundle is an empty folder needing an answer to "which
  modes are inside it", whose natural form is the whole-library fork of CONTENT_MODEL §1 one level
  down. An organization that wants its own bundle creates one, which 40.11 already allows.
- **The seeded hidden modes stay global, and the service refuses to override them** (409, not a
  silent no-op). `company-call` and `custom-scenario` prompts are half code: the service completes
  them at run time from placeholders it supplies, and a per-organization copy would drift away from
  the code that feeds it until it quietly stopped matching. `AiTenancyModelTests.Seeded_hidden_modes_stay_global_and_are_visible_to_every_organization` pins the
  invariant from the other side.
- **Resolution is applied to `GetActiveModesForBundleAsync` only.** That is the learner-facing mode
  list; without it an organization with an override would see the mode twice in the same bundle.
  Lookups by mode id resolve to themselves and need nothing.
- **Staleness is derived, and there is no Kafka event between services.** An override and the base it
  forked from are always the same content family in the same database, so staleness is an
  intra-database comparison everywhere it is asked. A cross-service message would add a delivery
  guarantee, an ordering question and a dead-letter path to a query that cannot be wrong.
- **Retiring an override is `IsActive = false`, not a new column and not a delete.** Mongo dialog
  sessions carry `ModeId` without a foreign key, so deleting the row to tidy a review queue would
  orphan every recorded conversation that used it. The mode list already filters on `IsActive`, and
  so does resolution, so an inactive override stops shadowing its base and the global prompt comes
  back — which is exactly what "take the new base" means.
- **`AiTenantTransactionScope`, new in this block, closes ai-service's long-standing gap.** It was
  the only service with RLS tables and no transaction scope: `TenantConnectionInterceptor` issues
  `SET LOCAL app.organization_id` when a transaction starts, and `SET LOCAL` does nothing outside
  one. While every bundle and mode was global that cost nothing — the content policy returns global
  rows even with the variable unset, and the global rows were all of them. The moment an
  organization owns a prompt, a read outside a transaction stops seeing it, and the administrator
  overrides a mode and then cannot find it. The override controller and `AdminDialogController` carry
  `[TenantTransaction]`; `GetActiveModesForBundleAsync` opens its own read scope.
- **Not in this block:** the review screen (no frontend was touched — 40.20), and prompt
  parameterization from an organization profile (40.19).

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
