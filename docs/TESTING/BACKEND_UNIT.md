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

## Block 5 — Voice dialog: structured chat output
**Files:** `Unit/StreamingChatReplyParserTests.cs`, `Unit/OpenAiChatServiceTests.cs`
**Status:** [x]

`StreamingChatReplyParser` — incremental extraction of `reply` from the streamed
JSON `{"reply", "endCall"}`:
- [x] `Parses_Simple_Reply_With_EndCall_False`
- [x] `Parses_EndCall_True`
- [x] `Emits_Same_Text_For_Any_Chunk_Size` (1 / 2 / 5 / 1000 byte deltas)
- [x] `Decodes_Escaped_Characters_Inside_Reply` (`\"`, `\n`, `\t`, `\uXXXX`)
- [x] `Handles_Reply_Key_Split_Across_Chunk_Boundary`
- [x] `Falls_Back_To_Plain_Text_When_Model_Ignores_Json_Contract`
- [x] `Fallback_Detects_Legacy_Dialog_End_Tag`
- [x] `Truncated_Json_Returns_Partial_Reply_Without_EndCall`
- [x] `Resolves_EndCall_From_Lenient_Match_When_Json_Is_Malformed`

`OpenAiChatService` — feedback parsing and honest XP:
- [x] `GenerateFeedbackAsync_WithXpTag_ParsesRewardAndBlocks`
- [x] `GenerateFeedbackAsync_WithoutXpTag_AwardsZero` (no silent 25 XP default)
- [x] `GenerateFeedbackAsync_ClampsXpToHundred`
- [x] `SendChatMessageAsync_ParsesStructuredReply`
- [x] `SendChatMessageAsync_FallsBackToPlainTextReply`

Run: `dotnet test --filter "FullyQualifiedName~StreamingChatReplyParser|FullyQualifiedName~OpenAiChatServiceTests"`
