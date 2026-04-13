# Refactor Progress

## Status Legend
- `[ ]` — not started
- `[>]` — in progress
- `[x]` — done
- `[~]` — blocked

---

## Phase 1: Analysis
Status: `[>]`

---

## Backend Files (C#)

### Features/Achievements
- [ ] Achievement.cs
- [ ] AchievementController.cs
- [ ] AchievementDto.cs
- [ ] AchievementSeeder.cs
- [ ] AchievementService.cs
- [ ] AchievementServiceCollectionExtensions.cs
- [ ] IAchievementService.cs
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
- [ ] AuthenticationServiceCollectionExtensions.cs
- [ ] DemoTokenController.cs
- [ ] GoogleLoginRequestDto.cs
- [ ] IAuthenticationService.cs
- [ ] IssuedTokenPair.cs
- [ ] LoginRequestDto.cs
- [ ] RefreshToken.cs
- [ ] RegisterRequestDto.cs
- [ ] SuperAdminSeeder.cs
- [ ] User.cs

### Features/Dialog
- [ ] AdminDialogController.cs
- [ ] ChatMessageResult.cs
- [ ] DialogBundle.cs
- [ ] DialogBundleDto.cs
- [ ] DialogController.cs
- [ ] DialogEntityConfigurations.cs
- [ ] DialogFeedbackResult.cs
- [ ] DialogMode.cs
- [ ] DialogModeDto.cs
- [ ] DialogRequestDtos.cs
- [ ] DialogSeeder.cs
- [ ] DialogService.cs
- [ ] DialogServiceCollectionExtensions.cs
- [ ] DialogSession.cs
- [ ] DialogSessionDto.cs
- [ ] FeedbackResult.cs
- [ ] IDialogService.cs
- [ ] IOpenAiChatService.cs
- [ ] OpenAiAuthenticationException.cs
- [ ] OpenAiChatService.cs
- [ ] OpenAiPaymentRequiredException.cs
- [ ] OpenAiRateLimitException.cs

### Features/Exercises
- [ ] ExerciseController.cs
- [ ] ExerciseDto.cs
- [ ] ExerciseEvaluationFactory.cs
- [ ] ExerciseEvaluationResult.cs
- [ ] ExerciseService.cs
- [ ] ExerciseServiceCollectionExtensions.cs
- [ ] ExerciseSubmissionResultDto.cs
- [ ] FillBlankEvaluationStrategy.cs
- [ ] IExerciseEvaluationStrategy.cs
- [ ] IExerciseService.cs
- [ ] LessonSummaryDto.cs
- [ ] MultipleChoiceEvaluationStrategy.cs
- [ ] NextLessonDto.cs
- [ ] OpenQuestionEvaluationStrategy.cs
- [ ] SubmitExerciseRequestDto.cs

### Features/Gamification
- [ ] GamificationServiceCollectionExtensions.cs
- [ ] StreakResetJob.cs
- [ ] UserStreak.cs
- [ ] UserXp.cs

### Features/League
- [ ] CurrentLeagueResponseDto.cs
- [ ] ILeagueService.cs
- [ ] League.cs
- [ ] LeagueController.cs
- [ ] LeagueMembership.cs
- [ ] LeagueParticipantDto.cs
- [ ] LeagueService.cs
- [ ] LeagueServiceCollectionExtensions.cs
- [ ] WeeklyLeagueClosureJob.cs

### Features/Lessons
- [ ] Exercise.cs
- [ ] Lesson.cs
- [ ] UserExerciseAttempt.cs
- [ ] UserLessonProgress.cs

### Features/Onboarding
- [ ] CompleteOnboardingRequestDto.cs
- [ ] IOnboardingService.cs
- [ ] OnboardingController.cs
- [ ] OnboardingService.cs
- [ ] OnboardingServiceCollectionExtensions.cs
- [ ] UserProfile.cs

### Features/Profile
- [ ] IProfileService.cs
- [ ] ProfileController.cs
- [ ] ProfileService.cs
- [ ] ProfileServiceCollectionExtensions.cs
- [ ] UpdatePersonaRequestDto.cs
- [ ] UserProfileStatsDto.cs

### Features/Reference
- [ ] IReferenceService.cs
- [ ] ReferenceController.cs
- [ ] ReferenceMaterial.cs
- [ ] ReferenceMaterialDto.cs
- [ ] ReferenceService.cs
- [ ] ReferenceServiceCollectionExtensions.cs

### Features/SkillTree
- [ ] ISkillTreeService.cs
- [ ] Skill.cs
- [ ] SkillTreeController.cs
- [ ] SkillTreeNodeDto.cs
- [ ] SkillTreeResponseDto.cs
- [ ] SkillTreeService.cs
- [ ] SkillTreeServiceCollectionExtensions.cs
- [ ] UpdateEnrolledSkillsRequestDto.cs
- [ ] UserSkillProgress.cs

### Features/Transcription
- [ ] ITranscriptionService.cs
- [ ] TranscriptionController.cs
- [ ] TranscriptionResult.cs
- [ ] TranscriptionServiceCollectionExtensions.cs
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
- [ ] VoiceMessageRequestDto.cs
- [ ] VoiceResponseDto.cs
- [ ] VoiceServiceCollectionExtensions.cs
- [ ] VoicerCreateTaskRequest.cs
- [ ] VoicerCreateTaskResponse.cs
- [ ] VoicerTaskStatusResponse.cs
- [ ] VoicerTemplate.cs
- [ ] VoicerTtsAuthenticationException.cs
- [ ] VoicerTtsException.cs
- [ ] VoicerTtsInsufficientFundsException.cs
- [ ] VoicerTtsRateLimitException.cs
- [ ] VoicerTtsService.cs
- [ ] VoicerTtsTimeoutException.cs
- [ ] VoicerVoiceSettings.cs

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

## Frontend Files (TypeScript/React)

### app/(admin)
- [ ] admin/bulk-lessons/page.tsx
- [ ] admin/content/page.tsx
- [ ] admin/dialog/[bundleId]/page.tsx
- [ ] admin/dialog/page.tsx
- [ ] admin/lessons/page.tsx
- [ ] admin/open-question/page.tsx
- [ ] admin/page.tsx
- [ ] admin/reference/page.tsx
- [ ] admin/seeder/page.tsx
- [ ] admin/skills/[id]/lessons/[lessonId]/exercises/page.tsx
- [ ] admin/skills/[id]/lessons/[lessonId]/page.tsx
- [ ] admin/skills/[id]/page.tsx
- [ ] admin/skills/[id]/reference/page.tsx
- [ ] admin/skills/page.tsx
- [ ] admin/users/page.tsx
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

### app/dialog
- [ ] [bundleId]/[modeId]/page.tsx
- [ ] [bundleId]/[modeId]/voice/page.tsx

### app/session
- [ ] [lessonId]/page.tsx

### app (root)
- [ ] api/logs/route.ts
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

### lib (root)
- [ ] clientLogger.ts
- [ ] logger.ts

---

## Summary

| Category | Total | Done |
|----------|-------|------|
| Backend (C#) | 127 | 0 |
| Frontend (TS/TSX) | 93 | 0 |
| **Total** | **220** | **0** |
