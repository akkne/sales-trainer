# Backend Unit Tests — Roadmap

All unit tests use EF Core InMemory DB. No Docker required.

## Block 1 — Evaluation Strategies
**File:** `Unit/EvaluationStrategies/`
**Status:** [x]

- [x] `MultipleChoice_CorrectIndex_ReturnsIsCorrectTrueScore100`
- [x] `MultipleChoice_WrongIndex_ReturnsIsCorrectFalseScore0`
- [x] `MultipleChoice_ExplanationPresent_ReturnedInResult`
- [x] `FillBlank_CorrectIndex_ReturnsIsCorrectTrue`
- [x] `FillBlank_WrongIndex_ReturnsIsCorrectFalse`
- [x] `Factory_MultipleChoiceType_ReturnsCorrectStrategy`
- [x] `Factory_FillBlankType_ReturnsCorrectStrategy`
- [x] `Factory_UnknownType_ThrowsNotSupportedException`

## Block 2 — AuthenticationService
**File:** `Unit/AuthenticationServiceTests.cs`
**Status:** [x]

- [x] `Register_NewEmail_CreatesUserReturnsTokenPair`
- [x] `Register_DuplicateEmail_ThrowsInvalidOperationException`
- [x] `Register_EmailNormalizedToLowercase`
- [x] `Login_ValidCredentials_ReturnsTokenPair`
- [x] `Login_UnknownEmail_ThrowsUnauthorizedAccessException`
- [x] `Login_WrongPassword_ThrowsUnauthorizedAccessException`
- [x] `Refresh_ValidToken_RevokesOldIssuesNew`
- [x] `Refresh_ExpiredToken_ThrowsUnauthorizedAccessException`
- [x] `Refresh_RevokedToken_ThrowsUnauthorizedAccessException`
- [x] `Revoke_ExistingToken_SetsIsRevokedTrue`

## Block 3 — ExerciseService
**File:** `Unit/ExerciseServiceTests.cs`
**Status:** [x]

- [x] `Submit_CorrectAnswer_AddsXpRecord`
- [x] `Submit_CorrectAnswer_CreatesLessonProgressCompleted`
- [x] `Submit_CorrectAnswer_UpdatesExistingLessonProgress`
- [x] `Submit_CorrectAnswer_LastLesson_SetsSkillCompleted`
- [x] `Submit_SkillCompleted_UnlocksNextPrerequisiteSkill`
- [x] `Submit_CorrectAnswer_FirstActivity_CreatesStreak`
- [x] `Submit_CorrectAnswer_ConsecutiveDay_IncrementsStreak`
- [x] `Submit_IncorrectAnswer_NoXpRecord`
- [x] `Submit_UnknownExercise_ThrowsKeyNotFoundException`

## Block 4 — LeagueService
**File:** `Unit/LeagueServiceTests.cs`
**Status:** [x]

- [x] `Close_RanksInDescendingXpOrder`
- [x] `Close_Top10_GetPromotedOutcome`
- [x] `Close_Bottom5_GetDemotedOutcome`
- [x] `Close_MiddleMembers_HaveNullPromotionOutcome`
- [x] `Close_CreatesNewLeagueForNextWeek`
- [x] `GetCurrent_NoLeagueExists_CreatesAndJoinsUser`
