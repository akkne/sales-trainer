# Phase 40 test backlog

Created 2026-08-18, when the owner lifted Rule #3 ("write no new tests", in force since
2026-08-16). Source of truth for *what* is missing: the section
"Тесты, которых нет (Правило №3, с 2026-08-16)" in [DONT_FORGET.md](../DONT_FORGET.md),
57 checklist items. This file is the *build plan* for those items — verified paths, the
project each lands in, and the packages the work splits into.

Every path below was verified to exist. Roughly **620 individual test cases** across
**57 items**.

## Repo-wide test conventions

All 11 test projects: **NUnit 4.2.2** + `NUnit3TestAdapter 4.6.0` +
`Microsoft.NET.Test.Sdk 17.11.1` + **FluentAssertions 6.12.1**, `net9.0`, nullable and
implicit usings on, `IsPackable=false`. **NSubstitute 5.3.0** everywhere except
`Gateway.Tests` and `BuildingBlocks.Tests`. **xUnit and Moq are used nowhere.** Files live
under `Unit/`, `Integration/`, `Helpers/` (Gateway.Tests and BuildingBlocks.Tests are flat).

Run tests per project — a solution-level `dotnet test` silently runs ~6% of the suite in
this repository:

```bash
for p in src/backend/*/*.Tests/*.csproj; do dotnet test "$p" --filter "TestCategory!=Integration"; done
```

Integration tests follow a **local-store-or-skip** pattern, not Testcontainers (except
`Identity.Tests`, which is the only project holding `Testcontainers.PostgreSql`). A new
integration test must `Assert.Ignore` when its store is unreachable, or the suite breaks on
machines without a database.

## Item table

`U` = pure unit (in-memory EF / substitutes), `INT` = needs a real store, `U+INT` = split
across two files. Paths are relative to `src/backend/`.

| # | block | project | class(es) under test | proposed file | cases | kind | already covered? |
|---|---|---|---|---|---|---|---|
| 1 | 40.25 | Gateway/new | `gateway/Gateway/appsettings.json` + all 72 `*Controller.cs` | `ControllerGatewayRouteParityTests.cs` | 3 | U | No — 10 route-flip files assert hand-listed names only |
| 2 | 40.26 | Learning + Notification | `Assignments/.../AssignmentDeadlineNoticeService.cs`; `Notification/Eventing/NotificationEventMapper.cs` | `Unit/AssignmentDeadlineNoticeServiceTests.cs` + append to `NotificationEventMapperTests.cs` | 4+1 | U | No |
| 3 | 40.26 | Learning | `AssignmentService.RemindAsync`, `AssignmentAudienceResolver`, `AdminAssignmentsController` | `Unit/AssignmentReminderScopeTests.cs` | 4 | U | No |
| 4 | 40.26 | Learning | `DialogReviews/.../DialogReviewService.PublishDisputeNoticesAsync` | `Unit/DialogReviewDisputeNoticeTests.cs` | 3 | U | No |
| 5 | 40.26 | **Identity** | `Membership/Endpoints/InternalMembershipsController.cs` | `Unit/InternalMembershipsControllerTests.cs` | 4 | U | No — claim verified, 0 hits |
| 6 | 40.25 | Learning | `Assignments/.../AssignmentDashboardService.cs` | `Unit/AssignmentDashboardServiceTests.cs` | 4 | U | No |
| 7 | 40.25 | Learning | `TeamInsights/.../TeamSkillMapService.cs` | `Unit/TeamSkillMapServiceTests.cs` | 4 | U | No |
| 8 | 40.25 | Learning | `DialogReviewService`, `DialogReviewsController`, `AdminDialogReviewsController` | `Unit/DialogReviewAuthorizationTests.cs` | 5 | U+INT | No |
| 9 | 40.25 | Learning | `AssignmentThresholdEvaluator.cs:178`, `Eventing/KafkaLearningEventPublisher.cs:84` | `Unit/AssignmentProgressEventEmissionTests.cs` | 2 | U | No |
| 10 | 40.25 | **Analytics** | `Funnels/.../FunnelEventRecorder.cs:93` | append to `Unit/FunnelEventRecorderTests.cs` | 1 | U | Partly — 7 tests exist, none on unknown `status` |
| 11 | 40.25 | **Ai** | `Dialog/.../IDialogSessionRepository.cs` + impl | `Integration/DialogSessionGradedQueryIntegrationTests.cs` | 3 | INT (Mongo) | Partly — reflective contract only |
| 12 | 40.14 | Gateway/new | cross-service, gateway + every store | `Integration/CrossServiceTenantIsolationE2ETests.cs` | 1 suite | INT | No — per-service isolation exists, nothing crosses the gateway |
| 13 | 40.14 | **5 projects** | `OutboxRelayBackgroundService`, `GamificationDialogWeightsConsumer`, `KafkaDialogEventPublisher`, `ExerciseDialogService`, both Mongo repos, `TenantSaveChangesInterceptor`, `OrganizationProfileService` | 6 files, see §D/P10 | 11 | U | Partly — system-mode bypass covered, `Guid.Empty` rejection not |
| 14 | 40.16 | Learning | `LessonAccuracyService`, `LessonVersionService`, `ExerciseService.cs:381` | `Integration/LessonAccuracyHistoryIntegrationTests.cs` | 2 | INT | No — roadmap `[~]` |
| 15 | 40.16 | Learning | `LessonAccuracyService`, `LessonVersionService.cs:195`, `LessonVersionBackfill` | 3 unit files + isolation | 17 | U+INT | Partly — collaborator only, no assertions |
| 16 | 40.15 | Learning | `LessonSnapshotSerializer`, `CanonicalJsonWriter`, `LessonVersionService`, `IX_Lessons_Slug_Global`, trigger `LessonVersions_reject_frozen_change` | 3 unit + 1 integration | 21 | U+INT | No |
| 17 | 40.17 | Learning | `ProgramVersionService`, `ProgramEnrollmentService`, `ProgramController`, `ProgramDiffDto` | 3 unit + 1 integration | 29 | U+INT | No |
| 18 | 40.18 | Learning + Ai | `ContentOverrideResolution`, `ContentAuthoringGuard`, `ContentSnapshotSerializer`, `ContentOverrideService`, `TenantTransactionAttribute`; `DialogModeOverrideService` | 4 unit + 1 integration + 1 Ai | 28 | U+INT | Partly — hidden modes covered |
| 19 | 40.19 | BuildingBlocks + Learning + Ai | `OrganizationPlaceholderRenderer`; both `OrganizationProfileProvider`; both `OrganizationProfileConsumer`; `AdminSeederController` | 6 files | 31 | U+INT | Partly — stub exists, not a subject |
| 20 | 40.21 | Learning | `Assignment`, `AssignmentCompletionRule`, `AssignmentDocumentSerializer`, `AssignmentCompletionRuleReader`, `AssignmentService` | 3 unit + 1 integration | 27 | U+INT | No |
| 21 | 40.22 | Learning + Ai | `AssignmentThresholdEvaluator`, `AssignmentCompletionRuleReader`, `UserDialogScore`, `DialogService.CompleteSessionAsync` | 2 unit + 1 integration + 1 Ai | 24 | U+INT | Partly — event JSON shape only |
| 22 | 40.23 | Learning + Notification + **frontend** | `AssignmentAudienceResolver`, `AssignmentFanOut`, `MyAssignmentService`, `AssignmentDeadlineSweepService`, `InternalAssignmentsController`, `NotificationEventMapper`; `active-assignment-card.tsx` | 4 unit + 1 integration + append + vitest | 32 | U+INT | Partly — mapper has 17 tests, none on assignments |
| 23 | 40.24 | Learning | `AssignmentRepeatIssueService`, `AssignmentRepeatScheduleReader`, `AssignmentFanOut`, `AssignmentRepeatSweepService`, `CK_Assignments_RepeatNoCascade` | 2 unit + 1 integration | 30 | U+INT | No |
| 24 | 40.27 | Learning + Ai | `ContentGenerationJobService`, `ContentGenerationStepRunner`, `ContentStructureDocumentSerializer`, `ContentGenerationSweepService`, `MaterialStructuringService`, `ExerciseGenerationService` | 3 unit + 1 integration | 26 | U+INT | No |
| 25 | 40.28 | Learning | `ContentSufficiencyInspector`, `ContentInsufficiencyDocumentSerializer`, `AiContentPipelineClient` | 3 unit + 1 integration | 26 | U+INT | No — roadmap `[~]` |
| 26 | 40.29 | **Organization** | `OrganizationProfileDraftMerger`, `OrganizationProfileFields` | `Unit/OrganizationProfileDraftMergerTests.cs` | 14 | U (pure) | No |
| 27 | 40.29 | Organization | `OrganizationProfileGapInspector`, `OrganizationProfileGapCodes` | `Unit/OrganizationProfileGapInspectorTests.cs` | 6 | U | No |
| 28 | 40.29 | Organization | `OrganizationProfileController` (`PATCH`), `OrganizationProfileService` | append to 2 existing files | 5 | U | Partly — `GET`/`PUT` covered |
| 29 | 40.29 | Organization | `OrganizationProfileController` authorization | append to `Unit/OrganizationControllerAuthorizationTests.cs` | 3 | U | Partly — **and partly stale, see §C-1** |
| 30 | 40.29 | Gateway | `organization-organizations` / `-root` routes | append to `OrganizationRouteFlipTests.cs` | 2 | U | Partly |
| 31 | 40.29 | Organization | `OrganizationProfileService` draft-preview leak | append to `Unit/OrganizationProfileServiceTests.cs` | 1 | U | Partly |
| 32 | 40.31 | Learning | `TeamSkillGapService.DetectCandidates` | `Unit/TeamSkillGapDetectionTests.cs` | 5 | U | No |
| 33 | 40.31 | Learning | `TeamSkillGapService`, `AdminTeamSkillGapsController` | `Unit/TeamSkillGapRequestIdempotencyTests.cs` | 4 | U | No |
| 34 | 40.31 | Learning | `AssignmentService.ResolveGeneratedSourceAsync`, `CK_Assignments_ManualHasNoSourceRef` | `Unit/AssignmentGeneratedSourceTests.cs` | 4 | U | No |
| 35 | 40.31 | Learning | `TeamSkillGapService`, `TeamSkillGapDismissal` | `Unit/TeamSkillGapDismissalTests.cs` | 5 | U(+1 INT) | No |
| 36 | 40.31 | Learning | `Common/Constants/SkillGapSourceRefs.cs` | `Unit/SkillGapSourceRefsTests.cs` | 4 | U (pure) | No |
| 37 | 40.31 | Learning | `TeamSkillGapMaterialComposer`, `ContentSufficiencyInspector` | `Unit/TeamSkillGapMaterialComposerTests.cs` | 4 | U | No |
| 38 | 40.31 | Learning | `AdminTeamSkillGapsController`, `AdminTeamInsightsController`, `TenantTransactionAttribute` | `Unit/AdminControllerAttributeContractTests.cs` | 2 | U (reflection) | No — existing policy test names no controller |
| 39 | 40.31 | Learning | `TeamSkillGapDismissal`, `ContentGenerationJob.GapSourceRef` | `Integration/TeamSkillGapDismissalIsolationIntegrationTests.cs` | 2 | INT | No |
| 40 | 40.32 | Learning | `ContentAdaptationJobService.ResolveTargetExerciseAsync`, `CreateOverrideAsync` | `Unit/ContentAdaptationExerciseMatchingTests.cs` | 5 | U | No |
| 41 | 40.32 | Learning | `ContentAdaptationJobService`, `ContentAdaptationItem`, `ContentSnapshotSerializer` | `Unit/ContentAdaptationStalenessTests.cs` | 6 | U | No |
| 42 | 40.32 | Learning | `CK_ContentAdaptationItems_Proposal` / `_Resolution` | `Integration/ContentAdaptationConstraintIntegrationTests.cs` | 3 | INT | No |
| 43 | 40.32 | Learning | `ContentFieldChangeSummarizer` | `Unit/ContentFieldChangeSummarizerTests.cs` | 7 | U (pure) | No |
| 44 | 40.32 | Learning | `ContentAdaptationStatusCalculator` | `Unit/ContentAdaptationStatusCalculatorTests.cs` | 7 | U (pure) | No |
| 45 | 40.32 | Learning | `ContentAdaptationStepRunner.ReadItemWorkAsync`, `ContentAdaptationSweepService` | 1 unit + 1 integration | 5 | U+INT | No |
| 46 | 40.32 | Learning | `ContentAdaptationJob`, `ContentAdaptationItem`, `UX_ContentAdaptationJobs_Live` | `Integration/ContentAdaptationIsolationIntegrationTests.cs` | 3 | INT | No |
| 47 | 40.32 | Learning | `AdminContentAdaptationController` | append to `Unit/AdminControllerAttributeContractTests.cs` | 2 | U (reflection) | No |
| 48 | 40.32 | Learning | `ExerciseContentValidator`, `ContentAdaptationStepRunner` | `Unit/ContentAdaptationProposalValidationTests.cs` | 3 | U | No |
| 49 | 40.33 | **Ai** | `Quotas/.../AiSpendMeter.RecordLlmUsageAsync` (`ON CONFLICT`) | `Integration/AiSpendMeterUsageIntegrationTests.cs` | 4 | INT (real Postgres — `ON CONFLICT` cannot run in-memory) | No — substituted only |
| 50 | 40.33 | Ai | `AiQuotaService`, `AiSpendMeter.EnsureLlmAllowanceAsync` | `Unit/AiQuotaThresholdTests.cs` | 5 | U | No |
| 51 | 40.33 | Ai | `AiQuotaService.ResolveAsync`, `OrganizationQuota` | `Unit/AiQuotaDefaultsTests.cs` | 4 | U | No |
| 52 | 40.33 | Ai | `VoiceUsageService`, `AiSpendMeter` reserve/refund | append to `Unit/VoiceReservationGateTests.cs` | 4 | U | Partly — **reuse this harness** |
| 53 | 40.33 | Learning (+Ai) | `Infrastructure/Ai/AiChatClient.cs`, `AiContentPipelineClient`, `InternalChatController` | `Integration/AiChatStreamSeamIntegrationTests.cs` | 4 | INT | Partly — error mapping done, streaming not |
| 54 | 40.33 | Ai | `OrganizationQuota`, `AiUsageRecord`, `AdminAiQuotaController` | `Integration/AiQuotaIsolationIntegrationTests.cs` | 3 | INT | No |
| 55 | 40.33 | Ai | `AiUnattributedCallException`, `AiQuotaExceptionHandler` | `Unit/AiUnattributedCallTests.cs` | 3 | U | No |
| 56 | 40.33 | Learning (+Ai) | both sweep services, `AiContentPipelineClient`, `AiQuotaPreflightController` | `Unit/ContentPipelinePreflightTests.cs` | 3 | U | No |
| 57 | 40.33 | Ai | `OpenAiUsageReader`, `AiSpendMeter.EstimateTokens` | `Unit/OpenAiUsageReaderTests.cs` | 3 | U (pure) | No |

**Distribution:** Learning.Tests 34 primary (+6 shared), Ai.Tests 9 (+5), Organization.Tests 6,
Gateway.Tests 3, Identity.Tests 1, Analytics.Tests 1; Notification.Tests, BuildingBlocks.Tests
and Social.Tests receive shared items only. **No item lands in Company.Tests or
Gamification.Tests.**

## §A — The gateway route-parity test (item #1, the one to write first)

### Config shape

`gateway/Gateway/appsettings.json` → `ReverseProxy` has `Routes` and `Clusters`. A route is
a named object with exactly `ClusterId` and `Match`:

```json
"learning-admin-assignments": {
  "ClusterId": "learning",
  "Match": { "Path": "/admin/assignments/{**catch-all}" }
}
```

`Match` only ever carries `Path`; no transforms, no host or method matching. Two path shapes:
`/prefix/{**catch-all}` and a bare `/prefix` (the `-root` variants, needed because a
catch-all does not match the collection URL itself).

**89 routes, 9 clusters today** — learning 45, identity 14, ai 10, gamification 8, social 5,
company 2, notification 2, organization 2, analytics 1.

**A naive "one prefix → one cluster" assertion produces a false failure:** `identity-profile`
owns `/profile/{**catch-all}` while `gamification-achievements` owns the more specific
`/profile/achievements`. `LearningRouteFlipTests.Profile_routes_are_not_captured_by_learning`
is the precedent for handling it.

### Controller side — four traps, each verified

72 `*Controller.cs` files; **58 non-internal `(service, prefix)` pairs**.

1. **26 controllers have no class-level `[Route]`** and carry the absolute path on the method
   (`[HttpGet("admin/assignments")]`). 25 are in learning-service, the 26th is
   `company-service/.../CompanyController.cs`. Templates must be composed as *class + method*,
   with the method template used verbatim when the class has none.
2. **9 controllers use a constant, not a literal** (`[Route(RouteConstants.OrganizationProfileBase)]`).
   **This is why the test must reflect over built assemblies, not regex the source** — a
   source regex sees `RouteConstants.X` and yields nothing. A regex prototype reported 10
   false "missing" prefixes and 1 false cluster mismatch for exactly this reason.
3. **`[HttpPost("/ai/tts")]` has a leading slash** (`Ai/Features/Dialog/InternalChatController.cs:140`),
   which in ASP.NET Core *discards* the class-level `[Route("ai/chat")]`.
4. **`AdminAiQuotaController.cs` declares two controller classes in one file.**
   File-per-controller assumptions fail.

### The exclusion rule must be widened

The item proposes excluding `internal/*` plus `healthz`/`metrics`. **Not sufficient** — five
ai-service controllers are service-to-service but sit under `ai/*` and are marked with
`[ServiceFilter(typeof(InternalServiceAuthFilter))]`: `InternalChatController` (`ai/chat`),
`ContentGenerationController` (`ai/content`), `ContentAdaptationController` (`ai/content`),
`AiQuotaPreflightController` (`ai/quota`), `EvaluationController` (`ai`).

**Correct predicate: prefix starts with `internal/` OR the controller carries
`InternalServiceAuthFilter`.** That is stronger anyway — it is tied to the thing that
actually makes a route unreachable from outside.

With that rule, **today's config has full parity: 0 missing routes, 0 cluster mismatches**
across all 58 prefixes. The 404 incident the item describes is fixed; the test's value is
entirely forward-looking.

### Where it lives

`Gateway.Tests` references only `Sellevate.Gateway.csproj` and has no NSubstitute. All 10
existing route-flip files spin `WebApplicationFactory<Program>` and assert with string keys
(`_configuration["ReverseProxy:Routes:learning-skills:ClusterId"]`).

- **(a) Add 9 `ProjectReference`s to `Gateway.Tests`** — cheapest, but pulling nine `Program`
  classes into one assembly creates an ambiguous-entry-point problem plus nine transitive
  dependency sets.
- **(b) A new `src/backend/route-parity/RouteParity.Tests`** referencing all 10 service
  projects, reading `gateway/Gateway/appsettings.json` from disk as a linked
  `CopyToOutputDirectory` item — no `WebApplicationFactory`, no `Program` collision.
  **Recommended.** It also gives item #12 a natural home.

## §B — Harnesses to reuse (do not reinvent)

**Learning.Tests** — 40 items touch it. EF InMemory + Relational + Npgsql; **no SQLite, no
Testcontainers**.
- `Unit/LearningDbContextFactory.cs` — `CreateInMemory(Guid? organizationId = null)`,
  `DefaultOrganizationId = aaaaaaaa-0000-4000-8000-000000000001`. Every new unit test needing
  a `LearningDbContext` goes through this.
- `Integration/LocalLearningPostgresTestSettings.cs` — `AdminConnectionString()`,
  `ApplicationRoleConnectionString()`, `IsReachableAsync()`,
  `TestDatabaseName = "learning_tenancy_integration_test"`. Local-Postgres-or-skip.
- `Helpers/StubOrganizationProfileProvider.cs` — already wired into 4 files. Items #19, #37
  reuse it.
- `Unit/LearningTenancyModelTests.cs` (7 reflective invariant tests) — many items'
  "strict vs content query-filter group" assertion belongs as a new `[TestCase]` here, not
  in a new file.

**Identity.Tests** — the only project with `Testcontainers.PostgreSql 3.10.0`.
- `Helpers/InMemoryDbContextFactory.Create(...)`; `Integration/IntegrationTestSetup.cs`
  (real `PostgreSqlContainer`); `Helpers/TestWebApplicationFactory.cs` with
  **`CreateAuthenticatedClient(...)` and `CreateOrganizationAdminClient(...)`**, plus
  `RecordingEmailSender` / `RecordingUserEventPublisher`.
- Item #5's `X-Internal-Service-Secret` half needs the HTTP pipeline → write it as an
  integration test.

**Organization.Tests** — 100% unit, no `Integration/` folder, no containers. All six items
here are correctly `U`. Only harness:
`Helpers/TestOrganizationDatabaseFactory.CreateInMemory(...)`. `OrganizationProfileDraftMerger`
and `OrganizationProfileGapInspector` are **pure functions** — items #26/#27 need no harness.

**Ai.Tests** — EF InMemory + Relational + Npgsql (added in 40.11 so isolation tests run
against real RLS).
- `Unit/AiDbContextFactory.CreateInMemory(...)`,
  `DefaultOrganizationId = aaaaaaaa-0000-4000-8000-00000000a111`, plus `EnterSystemMode`.
- `Integration/LocalAiStoreTestSettings.cs` — local-store-or-skip for **three** backends
  (Postgres, Mongo, Redis). Items #11, #49, #54 key off it.
- **Item #52 reuses `Unit/VoiceReservationGateTests.cs`**, whose `Build(...)` helper already
  fakes `IDatabase`/`IConnectionMultiplexer` Lua results in call order and already substitutes
  `IAiSpendMeter` + `IAiQuotaService` without asserting on them. Multiple `[TestFixture]`
  classes per file is accepted style here.

**Analytics.Tests** — no EF provider at all. `Helpers/AnalyticsWebApplicationFactory.cs`
exposes a substituted `IPresenceTracker` and `CreateAuthenticatedClient(...)`. **Item #10
goes into the existing `Unit/FunnelEventRecorderTests.cs`**, which already has the
Prometheus-counter helpers and the cardinality-cap test.

**Notification.Tests** — leanest csproj, pure unit only. `Unit/InMemoryNotificationStore.cs`
is the fake to reuse for dedup-key work. `NotificationEventMapperTests.cs` has 17 tests;
`Map_CompanyFollowUpDue_DedupesOnCompanyAndDueDate` is the exact template for the three
assignment dedup-key cases.

## §C — Stale or mis-filed items

**C-1 — Item #29 is partly stale.** Its claim that `PUT /organizations/profile` is not
separated from the class-level `[Authorize]` is no longer true:
`OrganizationProfileController.cs:58-59` now carries
`[Authorize(Policy = AuthorizationPolicies.RequireOrganizationAdministrator)]`, closed in
40.34 (commit `8c94827`). The test is still needed but must assert `RequireOrgAdmin` on
**three** methods (`PUT`, `PATCH`, `POST draft/apply`) and its absence on `GET` and
`POST draft` — not two methods plus a known hole. The companion item at `DONT_FORGET.md:2167`
("`PUT` доступен ЛЮБОМУ участнику организации — это дыра") is **fully stale**.

**C-2 — Item #1's framing is stale, the test is not.** All three `assignments` routes exist
today and a resolved sweep finds zero gaps. Writing the test to the item's literal exclusion
spec would produce five false failures on day one (see §A).

**C-3 — Items #24 and #25: the "маршруты гейтвея" sub-bullets are already satisfied.**
`learning-admin-content-generation` and `-root` both exist. These sub-bullets collapse into
item #1 and should be dropped from the case counts. Same for item #30's first half.

**C-4 — "восемь интеграционных тестов изоляции learning-service" is wrong.** Items #15, #16,
#17, #20, #22, #23 and #39 all cite it; `LearningTenantIsolationIntegrationTests.cs` has
**14** `[Test]` methods. The substance holds — it exercises only `Lessons`, `Exercises`,
`Skills`, `Topics`, `UserExerciseAttempts`, `UserLessonProgressRecords`,
`UserSkillProgressRecords`, `UserTechniqueProgress`, so `LessonVersions`, `ProgramVersions`,
`ProgramItems`, `ProgramEnrollments`, `Assignments`, `AssignmentProgressRecords`,
`UserDialogScores`, `OrganizationProfileReplicas`, `ContentGenerationJobs`,
`ContentAdaptationJobs`, `ContentAdaptationItems` and `TeamSkillGapDismissals` are genuinely
unknown to it — but anyone planning against "eight" will mis-scope. Item #12's "131
already-written isolation integration tests" needs re-counting before use as a baseline.

**C-5 — Item #22's last sub-bullet is mis-filed.** `ActiveAssignmentCard` is a React
component; it belongs in `src/frontend/__tests__/*.test.tsx` (vitest), not in any of the 11
.NET projects. Needs a separate frontend ticket.

**C-6 — Item #53 correctly self-reports as half-done.** `OpenAiProviderErrorTests` is already
rewritten onto `AiChatClient` with named-code `[TestCase]`s. Scope the item to the 4
streaming/NDJSON/WAV cases.

**Nothing in the list describes deleted code.** All 57 items resolve to real files, including
all 11 named DB constraints and every named method.

One naming trap: `EnsurePublishedVersionIdAsync` lives on **`ILessonVersionService`**
(`LessonVersionService.cs:195`) and is called from `ProgramVersionService.cs:323`,
`ExerciseService.cs:381` and `LessonVersionBackfill.cs:36`. Items #15 and #17 both reference
it and must not each build their own stub.

## §D — Work packages

Sized so that **no two packages write the same file**. The three append-target files
(`FunnelEventRecorderTests.cs`, `VoiceReservationGateTests.cs`,
`NotificationEventMapperTests.cs`) and the two shared-by-design files
(`AdminControllerAttributeContractTests.cs`, `OrganizationProfileServiceTests.cs`) are each
assigned to exactly one owner.

| package | items | projects | subject | ~cases |
|---|---|---|---|---|
| **P1** Gateway route parity + cross-service scaffolding | 1, 12, 30 | Gateway / new `RouteParity.Tests` | decides §A option (a) vs (b); #12 depends on the project existing | — |
| **P2** Assignment threshold, dashboard, funnel | 6, 9, 10, 20, 21 | Learning + Analytics + Ai | `AssignmentThresholdEvaluator`, `AssignmentDashboardService`, `AssignmentDocumentSerializer`, freeze trigger, `assignment.progress.changed` producer/consumer | 56 |
| **P3** Assignment fan-out, audience, repeats, reminders | 2, 3, 22, 23 | Learning + Notification | `AssignmentAudienceResolver`, `AssignmentFanOut`, `AssignmentRepeatIssueService`, `AssignmentDeadlineNoticeService`, both sweeps, 3 dedup keys. **Owns all Notification.Tests work** | 71 |
| **P4** Lesson and programme versioning | 14, 15, 16, 17 | Learning | `CanonicalJsonWriter`, `LessonSnapshotSerializer`, `LessonVersionService`, `LessonAccuracyService`, `ProgramVersionService`, freeze triggers, partial unique indexes. **Most integration-heavy** | 69 |
| **P5** Content overrides + organization parameterization | 18, 19 | Learning + BuildingBlocks + Ai | `ContentOverrideResolution`, `ContentAuthoringGuard`, `OrganizationPlaceholderRenderer`, both providers, both consumers, `banned_claims` | 59 |
| **P6** Content generation pipeline + sufficiency gate | 24, 25, 56 | Learning + Ai | `ContentGenerationJobService`, `ContentSufficiencyInspector`, `AiContentPipelineClient` preflight | 55 |
| **P7** Content adaptation batches | 40–48 | Learning | the whole `Features/ContentAdaptation` tree, 2 CK constraints, controller attributes. **Creates `AdminControllerAttributeContractTests.cs`** | 41 |
| **P8** Team skill-gap detection | 7, 32–39 | Learning | `TeamSkillGapService`, `TeamSkillGapMaterialComposer`, `SkillGapSourceRefs`, `TeamSkillMapService`, dismissal isolation. **Appends to P7's file — sequence after P7** | 34 |
| **P9** AI quota, spend metering, voice windows | 49–55, 57 | Ai (+Learning for #53) | `AiSpendMeter`, `AiQuotaService`, `OpenAiUsageReader`, `VoiceUsageService`, quota isolation. **Owns `VoiceReservationGateTests.cs` appends** | 30 |
| **P10** Organization profile, identity boundary, cross-cutting tenancy | 5, 11, 13, 26–29, 31 | Organization + Identity + BuildingBlocks + Ai + Social | everything outside learning-service. **Owns appends to `OrganizationProfileServiceTests.cs`, `OrganizationProfileControllerTests.cs`, `OrganizationControllerAuthorizationTests.cs`, `TenantSaveChangesInterceptorTests.cs`** | 48 |

**Order:** P10 and P1 first — P10 for cheap wins on non-overlapping projects (#26 and #27 are
pure functions with zero harness, the highest value per hour in the backlog), P1 because the
source document says item #1 is the one to write first. Then P2, P4, P7, P9 in parallel. Then
P3, P5, P6, P8. Only **P7 → P8** has a file-level ordering constraint.

Item #22's frontend sub-bullet is not assignable to any package — separate frontend ticket.
