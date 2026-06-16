# Backend Integration Tests — Roadmap

All integration tests use a real Postgres container (Testcontainers) + WebApplicationFactory.
**Requires Docker.**

## Infrastructure
- `Helpers/TestWebApplicationFactory.cs` — overrides connection string, JWT config, Hangfire storage
- `Helpers/IntegrationTestSetup.cs` — `[SetUpFixture]` starts Postgres container once for all tests
- `Helpers/JwtTestHelper.cs` — builds test JWTs with known signing key
- `Helpers/TestDbSeeder.cs` — seeds minimal test data per test

## Block 1 — Auth Endpoints
**File:** `Integration/AuthTests.cs`
**Status:** [x]

- [x] `Register_ValidData_Returns200WithAccessToken`
- [x] `Register_DuplicateEmail_Returns409`
- [x] `Login_ValidCredentials_Returns200AndSetsRefreshCookie`
- [x] `Login_WrongPassword_Returns401`
- [x] `Refresh_ValidCookie_Returns200WithNewAccessToken`
- [x] `Refresh_NoCookie_Returns401`
- [x] `Logout_RevokesToken_DeletesCookie`
- [x] `Me_ValidJwt_ReturnsCurrentUserInfo`

## Block 2 — Exercise Endpoints
**File:** `Integration/ExerciseSubmitTests.cs`
**Status:** [x]

- [x] `GetLessons_ValidToken_Returns200WithList`
- [x] `GetLessons_UnknownSkillSlug_Returns404`
- [x] `GetLessons_NoToken_Returns401`
- [x] `GetExercises_ValidLessonId_Returns200WithList`
- [x] `Submit_CorrectAnswer_Returns200WithIsCorrectTrue`
- [x] `Submit_CorrectAnswer_XpGrantedToUser`
- [x] `Submit_IncorrectAnswer_Returns200WithIsCorrectFalse`
- [x] `Submit_UnknownExerciseId_Returns404`

## Block 3 — Onboarding + SkillTree
**File:** `Integration/OnboardingAndSkillTreeTests.cs`
**Status:** [x]

Note: these tests verify `text[]` PostgreSQL array containment — only works with real Postgres.

- [x] `Onboarding_ValidToken_Returns204`
- [x] `Onboarding_CreatesSkillProgressForApplicableTypes`
- [x] `Onboarding_FirstSkillIsAvailable_RestLocked`
- [x] `Onboarding_AlreadyCompleted_IsIdempotent`
- [x] `SkillTree_ValidToken_ReturnsNodes`
- [x] `SkillTree_AggregatesXpAndStreak`
- [x] `SkillTree_NoToken_Returns401`

## Block 4 — Admin CRUD + Auth Policies
**File:** `Integration/AdminSkillsTests.cs`, `Integration/AdminUsersTests.cs`
**Status:** [x]

- [x] `GetSkills_AdminToken_Returns200`
- [x] `GetSkills_UserToken_Returns403`
- [x] `GetSkills_NoToken_Returns401` _(covered by RequireAdmin policy)_
- [x] `CreateSkill_AdminToken_Returns200WithCreatedSkill`
- [x] `UpdateSkill_AdminToken_Returns200WithUpdatedData`
- [x] `UpdateSkill_UnknownId_Returns404`
- [x] `DeleteSkill_AdminToken_Returns204`

**AdminUsers** (`Integration/AdminUsersTests.cs`) — list/detail, moderation rename, photo reset, role change:
- [x] `GetAll_AsSuperAdmin_Returns200WithUsers` / `GetAll_AsAdmin_Returns200`
- [x] `GetById_ReturnsRicherUserDetail` _(email, authProvider, hasCustomAvatar, activity stats)_
- [x] `GetById_NonExistentUser_Returns404`
- [x] `UpdateUser_AsAdmin_RenamesDisplayName` _(moderation rename)_
- [x] `UpdateUser_TooShortName_Returns400` / `UpdateUser_NonExistentUser_Returns404`
- [x] `DeleteAvatar_AsAdmin_Returns204` / `DeleteAvatar_NonExistentUser_Returns404` _(moderation photo reset)_
- [x] `ChangeRole_AsSuperAdmin_Returns200WithNewRole` / `ChangeRole_AsAdmin_Returns403` _(SuperAdmin-only)_
- [x] `ChangeRole_InvalidRole_Returns400` / `ChangeRole_NonExistentUser_Returns404`

## Block 5 — Admin Leagues
**File:** `Integration/AdminLeaguesTests.cs`
**Status:** [x]

- [x] `GetLeagues_AsAdmin_Returns200`
- [x] `GetLeagues_AsUser_Returns403`
- [x] `GetLeagueDetail_ReturnsMembersWithUserInfo`
- [x] `AdjustXp_CreatesCorrectionRecordAndSurvivesResync`
- [x] `AdjustXp_NonExistentMembership_Returns404`
- [x] `MoveTier_MovesToSameWeekLeague_CreatingIfMissing`
- [x] `MoveTier_ClearsStalePromotionOutcome` _(regression: outcome badge must not follow member across tiers)_
- [x] `MoveTier_InvalidTier_Returns400`
- [x] `RemoveMembership_Returns204AndDeletesRow`
- [x] `CloseCurrent_Returns204`
- [x] `UpdateSettings_PersistsValues`
- [x] `UpdateSettings_ZonesExceedMax_Returns400`
- [x] `UpdateSettings_PersistsPeriodEndAndLength` _(admin-set period schedule)_
- [x] `GetTiers_ReturnsSeededLadder`
- [x] `CreateTier_ThenAppearsInList`
- [x] `CreateTier_DuplicateKey_Returns400`
- [x] `UpdateTier_ChangesNameColorOrder`
- [x] `DeleteTier_WithExistingLeagues_Returns400`
- [x] `DeleteTier_WithoutLeagues_Returns204`
- [x] `TierEndpoints_AsUser_Returns403`

## Block 5b — Admin Topics (update by GUID)
**File:** `Integration/AdminTopicsTests.cs`
**Status:** [x]

- [x] `UpdateById_AsAdmin_PersistsChanges` _(regression: `PUT /admin/topics/:id` was missing → all topic edits 404'd)_
- [x] `UpdateById_NonExistent_Returns404`
- [x] `UpdateById_DuplicateIconicName_Returns409`

## Block 5d — Admin Skill Stages (DB-driven funnel stages)
**File:** `Integration/AdminSkillStagesTests.cs`
**Status:** [x]

- [x] `GetAll_AsAdmin_Returns200WithList` / `GetAll_AsRegularUser_Returns403`
- [x] `Create_AsAdmin_PersistsStageAndLowercasesKey`
- [x] `Create_DuplicateKey_Returns400` / `Create_MissingLabel_Returns400`
- [x] `Update_AsAdmin_ChangesLabelAccentOrderButNotKey` (key is immutable) / `Update_UnknownId_Returns404`
- [x] `Delete_StageWithAssignedSkills_Returns400` / `Delete_UnusedStage_Returns204`
- [x] `PublicStages_AsUser_Returns200OrderedByOrder` (`GET /skills/stages`)

## Block 5c — Admin Exercises Bulk Import
**File:** `Integration/AdminExercisesImportTests.cs`
**Status:** [x]

- [x] `Import_AcceptsArray_CreatesAndUpdatesByOrder`
- [x] `Import_EmptyArray_Returns400`
- [x] `Import_NonExistentLesson_Returns404`

## Block 6 — Daily Quotes
**File:** `Integration/AdminDailyQuotesTests.cs`
**Status:** [x]

- [x] `GetAll_AsAdmin_FiltersByDateRange`
- [x] `GetAll_AsRegularUser_Returns403`
- [x] `Create_AsAdmin_Returns200WithCreatedQuote`
- [x] `Create_DuplicateDate_Returns409`
- [x] `Create_EmptyText_Returns400`
- [x] `Update_AsAdmin_Returns200WithUpdatedData`
- [x] `Update_NonExistentQuote_Returns404`
- [x] `Delete_AsAdmin_Returns204`
- [x] `Delete_NonExistentQuote_Returns404`
- [x] `PublicEndpoint_ReturnsQuoteForDate_AndFallsBackToEarlier`
