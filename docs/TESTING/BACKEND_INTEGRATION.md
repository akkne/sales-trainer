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

## Block 5 — Admin Leagues
**File:** `Integration/AdminLeaguesTests.cs`
**Status:** [x]

- [x] `GetLeagues_AsAdmin_Returns200`
- [x] `GetLeagues_AsUser_Returns403`
- [x] `GetLeagueDetail_ReturnsMembersWithUserInfo`
- [x] `AdjustXp_CreatesCorrectionRecordAndSurvivesResync`
- [x] `AdjustXp_NonExistentMembership_Returns404`
- [x] `MoveTier_MovesToSameWeekLeague_CreatingIfMissing`
- [x] `MoveTier_InvalidTier_Returns400`
- [x] `RemoveMembership_Returns204AndDeletesRow`
- [x] `CloseCurrent_Returns204`
- [x] `UpdateSettings_PersistsValues`
- [x] `UpdateSettings_ZonesExceedMax_Returns400`
