# Refactoring Progress

## Status Legend
- `[x]` — done
- `[>]` — in progress
- `[ ]` — not started
- `[~]` — blocked (reason next to it)

---

## Phase 1: Analysis

### [x] Initial setup
- [x] Read project structure
- [x] Create REFACTOR_PROGRESS.md
- [ ] Update CODESTYLE.md
- [ ] Initial checkpoint commit

---

## Backend Files (src/backend/api)

### Features/Achievements
- [ ] Achievement.cs
- [ ] AchievementController.cs
- [ ] AchievementDto.cs
- [ ] AchievementSeeder.cs
- [ ] AchievementService.cs
- [ ] UserAchievement.cs

### Features/Admin
- [ ] AdminExercisesController.cs
- [ ] AdminLessonsController.cs
- [ ] AdminOpenQuestionController.cs
- [ ] AdminReferenceController.cs
- [ ] AdminSeederController.cs
- [ ] AdminSkillsController.cs
- [ ] AdminUsersController.cs

### Features/Auth
- [ ] AuthController.cs
- [ ] AuthTokenResponseDto.cs
- [ ] AuthenticationService.cs
- [ ] DemoTokenController.cs
- [ ] GoogleLoginRequestDto.cs
- [ ] IssuedTokenPair.cs
- [ ] LoginRequestDto.cs
- [ ] RefreshToken.cs
- [ ] RegisterRequestDto.cs
- [ ] SuperAdminSeeder.cs
- [ ] User.cs

### Features/Dialog
- [ ] AdminDialogController.cs
- [ ] DialogBundle.cs
- [ ] DialogBundleDto.cs
- [ ] DialogController.cs
- [ ] DialogEntityConfigurations.cs
- [ ] DialogMode.cs
- [ ] DialogModeDto.cs
- [ ] DialogRequestDtos.cs
- [ ] DialogSeeder.cs
- [ ] DialogService.cs
- [ ] DialogSession.cs
- [ ] DialogSessionDto.cs
- [ ] IOpenAiChatService.cs
- [ ] OpenAiChatService.cs

### Features/Exercises
- [ ] ExerciseController.cs
- [ ] ExerciseDto.cs
- [ ] ExerciseEvaluationFactory.cs
- [ ] ExerciseEvaluationResult.cs
- [ ] ExerciseService.cs
- [ ] ExerciseSubmissionResultDto.cs
- [ ] FillBlankEvaluationStrategy.cs
- [ ] IExerciseEvaluationStrategy.cs
- [ ] LessonSummaryDto.cs
- [ ] MultipleChoiceEvaluationStrategy.cs
- [ ] NextLessonDto.cs
- [ ] OpenQuestionEvaluationStrategy.cs
- [ ] SubmitExerciseRequestDto.cs

### Features/Gamification
- [ ] StreakResetJob.cs
- [ ] UserStreak.cs
- [ ] UserXp.cs

### Features/League
- [ ] CurrentLeagueResponseDto.cs
- [ ] League.cs
- [ ] LeagueController.cs
- [ ] LeagueMembership.cs
- [ ] LeagueParticipantDto.cs
- [ ] LeagueService.cs
- [ ] WeeklyLeagueClosureJob.cs

### Features/Lessons
- [ ] Exercise.cs
- [ ] Lesson.cs
- [ ] UserExerciseAttempt.cs
- [ ] UserLessonProgress.cs

### Features/Onboarding
- [ ] CompleteOnboardingRequestDto.cs
- [ ] OnboardingController.cs
- [ ] OnboardingService.cs
- [ ] UserProfile.cs

### Features/Profile
- [ ] ProfileController.cs
- [ ] ProfileService.cs
- [ ] UpdatePersonaRequestDto.cs
- [ ] UserProfileStatsDto.cs

### Features/Reference
- [ ] ReferenceController.cs
- [ ] ReferenceMaterial.cs
- [ ] ReferenceMaterialDto.cs
- [ ] ReferenceService.cs

### Features/SkillTree
- [ ] Skill.cs
- [ ] SkillTreeController.cs
- [ ] SkillTreeNodeDto.cs
- [ ] SkillTreeResponseDto.cs
- [ ] SkillTreeService.cs
- [ ] UpdateEnrolledSkillsRequestDto.cs
- [ ] UserSkillProgress.cs

### Features/Transcription
- [ ] ITranscriptionService.cs
- [ ] TranscriptionController.cs
- [ ] WhisperTranscriptionService.cs

### Features/Voice
- [ ] GoogleTtsService.cs
- [ ] IGoogleTtsService.cs
- [ ] IVoiceDialogService.cs
- [ ] IVoicerTtsService.cs
- [ ] VoiceConfigController.cs
- [ ] VoiceConfigDto.cs
- [ ] VoiceDialogController.cs
- [ ] VoiceDialogService.cs
- [ ] VoicerTtsService.cs

### Infrastructure/Data
- [ ] AppDbContext.cs
- [ ] ExerciseEntityConfiguration.cs
- [ ] OpenQuestionGlobalContextConfiguration.cs
- [ ] SkillEntityConfiguration.cs
- [ ] UserExerciseAttemptEntityConfiguration.cs

### Infrastructure/Mongo
- [ ] MongoDbContext.cs

### Root
- [ ] Program.cs

---

## Backend Tests (src/backend/tests)

### Helpers
- [ ] InMemoryDbContextFactory.cs
- [ ] IntegrationTestSetup.cs
- [ ] JwtTestHelper.cs
- [ ] TestDbSeeder.cs
- [ ] TestWebApplicationFactory.cs

### Integration
- [ ] AdminSkillsTests.cs
- [ ] AdminUsersTests.cs
- [ ] AuthTests.cs
- [ ] ExerciseSubmitTests.cs
- [ ] OnboardingAndSkillTreeTests.cs
- [ ] VoicerTtsApiTests.cs

### Unit
- [ ] AuthenticationServiceTests.cs
- [ ] ExerciseServiceTests.cs
- [ ] LeagueServiceTests.cs

### Unit/EvaluationStrategies
- [ ] ExerciseEvaluationFactoryTests.cs
- [ ] FillBlankEvaluationStrategyTests.cs
- [ ] MultipleChoiceEvaluationStrategyTests.cs

---

## Frontend Files (src/frontend)

### app/(admin)/admin
- [ ] bulk-lessons/page.tsx
- [ ] content/page.tsx
- [ ] dialog/[bundleId]/page.tsx
- [ ] dialog/page.tsx
- [ ] lessons/page.tsx
- [ ] open-question/page.tsx
- [ ] page.tsx
- [ ] reference/page.tsx
- [ ] seeder/page.tsx
- [ ] skills/[id]/lessons/[lessonId]/exercises/page.tsx
- [ ] skills/[id]/lessons/[lessonId]/page.tsx
- [ ] skills/[id]/page.tsx
- [ ] skills/[id]/reference/page.tsx
- [ ] skills/page.tsx
- [ ] users/page.tsx
- [ ] layout.tsx

### app/(auth)
- [ ] layout.tsx
- [ ] login/page.tsx
- [ ] onboarding/page.tsx
- [ ] register/page.tsx

### app/(main)
- [ ] dialog/[bundleId]/page.tsx
- [ ] dialog/page.tsx
- [ ] exercise/[id]/page.tsx
- [ ] guidebook/page.tsx
- [ ] layout.tsx
- [ ] league/page.tsx
- [ ] profile/page.tsx
- [ ] reference/[id]/page.tsx
- [ ] skill/[id]/map/page.tsx
- [ ] skill/[id]/page.tsx
- [ ] tree/page.tsx

### app/api
- [ ] logs/route.ts

### app/dialog
- [ ] [bundleId]/[modeId]/page.tsx
- [ ] [bundleId]/[modeId]/voice/page.tsx

### app/session
- [ ] [lessonId]/page.tsx

### app root
- [ ] layout.tsx
- [ ] page.tsx
- [ ] providers.tsx

### components/dialog
- [ ] BundleCard.tsx
- [ ] ChatInput.tsx
- [ ] ChatMessage.tsx
- [ ] DeleteConfirmModal.tsx
- [ ] FeedbackModal.tsx
- [ ] ModeCard.tsx
- [ ] SessionHistorySidebar.tsx
- [ ] VoiceMicButton.tsx

### components/exercise
- [ ] ExerciseResultBanner.tsx
- [ ] FillBlankExercise.tsx
- [ ] MultipleChoiceExercise.tsx
- [ ] OpenQuestionExercise.tsx

### components/layout
- [ ] BottomNav.tsx
- [ ] Sidebar.tsx
- [ ] StatsWidget.tsx
- [ ] TopAppBar.tsx

### components/ui
- [ ] AchievementToast.tsx
- [ ] Button.tsx
- [ ] Card.tsx
- [ ] Common.tsx
- [ ] GoogleLoginButton.tsx
- [ ] Icon.tsx
- [ ] Input.tsx
- [ ] LessonPath.tsx
- [ ] Progress.tsx
- [ ] SkillNode.tsx
- [ ] index.ts

### lib/api
- [ ] apiClient.ts

### lib/hooks
- [ ] useAchievements.ts
- [ ] useAdmin.ts
- [ ] useAdminDialog.ts
- [ ] useAuth.ts
- [ ] useDialog.ts
- [ ] useKeyboardControls.ts
- [ ] useLeague.ts
- [ ] useLesson.ts
- [ ] useOnboarding.ts
- [ ] useProfile.ts
- [ ] useReference.ts
- [ ] useSkillTree.ts
- [ ] useVoice.ts

### lib/store
- [ ] authStore.ts
- [ ] selectedSkillStore.ts

### lib/voice
- [ ] audioPlayer.ts
- [ ] deepgramClient.ts
- [ ] vadManager.ts
- [ ] webSpeechClient.ts

### lib root
- [ ] clientLogger.ts
- [ ] logger.ts

### config
- [ ] next.config.ts

### tests
- [ ] __tests__/AchievementToast.test.tsx
- [ ] __tests__/LessonPath.test.tsx
- [ ] __tests__/MultipleChoiceExercise.test.tsx
- [ ] __tests__/countdown.test.ts
- [ ] __tests__/sessionStats.test.ts
- [ ] __tests__/useKeyboardControls.test.ts
- [ ] vitest.setup.ts

---

## Summary

| Area | Total Files | Done | Remaining |
|------|-------------|------|-----------|
| Backend API | 87 | 0 | 87 |
| Backend Tests | 14 | 0 | 14 |
| Frontend | 70 | 0 | 70 |
| **Total** | **171** | **0** | **171** |

---

## Issues Found

See `REFACTOR_ISSUES.md` for any bugs, security issues, or unclear code discovered during refactoring.
