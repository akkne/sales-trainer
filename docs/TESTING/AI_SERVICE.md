# TESTING — AI Engine Service

How to build, test, and manually verify `src/backend/ai-service`.

## Automated tests

```bash
dotnet test src/backend/ai-service/Ai.Tests/Sellevate.Ai.Tests.csproj
```

Unit suite (NUnit, no external dependencies — runs offline):

| Test fixture | Covers |
|---|---|
| `DialogScoringWeightsProviderTests` | Default weights (25/25/25/25 ×1.0); update replaces the cache; null guard. |
| `GamificationDialogWeights` flow | Weights cache is refreshed from the Kafka payload (provider contract). |
| `ExerciseEvaluationFactoryTests` | Strategy lookup by exercise type; unknown type throws `NotSupportedException`. |
| `SpotMistakeEvaluationStrategyTests` | Local line-selection scoring (wrong line → 0, correct line w/o explanation → 50) without calling the LLM. |
| `StreamingChatReplyParserTests` | Incremental `reply` extraction from streamed JSON, chunked reassembly, `endCall`, plain-text fallback. |
| `SentenceChunkerTests` | Sentence-ender splitting, minimum-length buffering, tail drain. |
| `AdminDialogExportTests` | `GET /admin/dialog/export` returns bundles with nested modes (ordered by SortOrder) in the re-importable `{ bundles: [...] }` shape. |

## Build

```bash
dotnet build src/backend/ai-service/Ai/Sellevate.Ai.csproj
```

## Manual checklist (requires infra + API keys)

1. `scripts/dev-infra.sh` then `scripts/dev-ai.sh` (or `docker compose up --build -d ai gateway`).
2. `GET http://localhost:5003/healthz` → `{ "status": "ok", "service": "ai" }`.
3. Through the gateway (`http://localhost:5000`):
   - `GET /dialog/bundles` (empty `[]` when `OpenAI:ApiKey` unset — graceful degradation).
   - Create a bundle/mode via `POST /admin/dialog/bundles` (+ `/modes`), then start a
     session, send a message, complete it → feedback + `xpEarned`.
   - `POST /transcription/transcribe` (multipart audio) → `{ text, language }`.
   - Voice stream `POST /dialog/sessions/{id}/voice/stream` → length-prefixed frames.
4. Confirm a completed session emits `dialog.evaluated` (Kafka UI on `:8085`).
5. Publish a `gamification.dialog-weights.updated` event and confirm the next session's
   XP reflects the new weights/multiplier.
6. `POST /ai/evaluate` (internal, hit `http://localhost:5003/ai/evaluate` directly) with
   `{ exerciseType, systemPrompt, exerciseContent, userAnswer }` → `{ isCorrect, score, ... }`.
