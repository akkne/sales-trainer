# TESTING — Learning Service

> How to run and what is covered for the Phase 8 `learning-service`.
> See [LEARNING_SERVICE.md](../LEARNING_SERVICE.md) for the design.

## Run

```bash
# all backend tests
dotnet test src/backend/Sellevate.sln

# just the learning service
dotnet test src/backend/learning-service/Learning.Tests/Sellevate.Learning.Tests.csproj

# gateway route-flip tests (learning routes)
dotnet test src/backend/gateway/Gateway.Tests/Sellevate.Gateway.Tests.csproj
```

The unit tests are offline/mocked (NUnit + FluentAssertions + NSubstitute + EF Core
InMemory). No Postgres/Kafka/Redis/AI service is required.

## Coverage

| Area | Test fixture |
|---|---|
| Deterministic grading (choose_option, fill_blank, reorder, match_pairs partial credit, categorize, theory_card) | `DeterministicEvaluationStrategyTests` |
| AI grading path delegates to ai-service `/ai/evaluate` with the global prompt, maps the verdict | `AiExerciseEvaluationStrategyTests` (mocks `IAiEvaluationClient`) |
| Submission emits `exercise.completed` always, `lesson.completed` + `skill.completed` on completion; no events of the latter two on a wrong answer; `XpEarned` is 0 | `ExerciseServiceEventEmissionTests` (mocks `ILearningEventPublisher`) |
| Skill-tree progress aggregation (completed/total lessons, status) and gamification aggregates returned as 0 | `SkillTreeServiceTests` |
| Technique cards (IsNew flag), MarkTechniqueSeen creates progress once (idempotent) | `TechniqueServiceTests` |
| Admin technique export returns all techniques (ordered by SortOrder) in the re-importable `AdminTechniqueWriteRequestDto[]` shape, preserving tags/dialog/case/coach | `AdminTechniquesExportTests` |
| Seeder content export (skills, topics, lessons, bundle) returns the re-importable seeder shapes: icon names resolved from ids, exercises nested, exercise `content` emitted as a JSON object | `AdminSeederExportTests` |
| Produced event payload shapes match the gamification consumer contract + canonical topic names | `OutgoingEventContractTests` |
| Gateway flips `/skills`, `/skill-tree`, `/lessons`, `/topics`, `/exercises`, `/reference`, `/techniques`, `/daily-quote`, learning `/admin/*` to the learning cluster and not the monolith; `/profile` is not captured | `Gateway.Tests/LearningRouteFlipTests` |

## Manual smoke (full stack)

```bash
scripts/dev-infra.sh        # postgres, redis, kafka, loki
scripts/dev-ai.sh           # AI grading endpoint
scripts/dev-learning.sh     # learning on :5008, db "learning" auto-created
scripts/dev-gateway.sh      # routes flipped to learning

curl http://localhost:5008/healthz
# через шлюз (с валидным JWT):
curl -H "Authorization: Bearer <token>" http://localhost:5000/skills
curl -H "Authorization: Bearer <token>" http://localhost:5000/skill-tree
curl -H "Authorization: Bearer <token>" http://localhost:5000/daily-quote
```
