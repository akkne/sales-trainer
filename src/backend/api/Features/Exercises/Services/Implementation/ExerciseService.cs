using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SalesTrainer.Api.Features.Achievements.Services.Abstract;
using SalesTrainer.Api.Features.Dialog.Models;
using SalesTrainer.Api.Features.Dialog.Services.Abstract;
using SalesTrainer.Api.Features.Dialog.Services.Implementation;
using SalesTrainer.Api.Features.Exercises.Models;
using SalesTrainer.Api.Features.Exercises.Services.Abstract;
using SalesTrainer.Api.Features.Gamification.Models;
using SalesTrainer.Api.Features.Gamification.Services.Abstract;
using SalesTrainer.Api.Features.Lessons.Models;
using SalesTrainer.Api.Features.Notifications.Models;
using SalesTrainer.Api.Features.Notifications.Services.Abstract;
using SalesTrainer.Api.Features.Voice.Services.Abstract;
using SalesTrainer.Api.Features.Voice.Services.Implementation;
using SalesTrainer.Api.Infrastructure.Data;

namespace SalesTrainer.Api.Features.Exercises.Services.Implementation;

internal sealed class ExerciseService(
    AppDbContext databaseContext,
    ExerciseEvaluationFactory evaluationFactory,
    IAchievementService achievementService,
    IOpenAiChatService openAiChatService,
    INotificationService notificationService,
    IGamificationService gamificationService,
    ITtsRouter ttsRouter,
    ILogger<ExerciseService> logger) : IExerciseService
{
    public async Task<IReadOnlyList<LessonSummaryDto>> GetAllLessonsAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var lessonProgressByLessonId = await databaseContext.UserLessonProgressRecords
            .Where(progressRecord => progressRecord.UserId == userId)
            .ToDictionaryAsync(progressRecord => progressRecord.LessonId, cancellationToken);

        var allLessons = await databaseContext.Lessons
            .OrderBy(lesson => lesson.OrderInTopic)
            .ThenBy(lesson => lesson.Id)
            .ToListAsync(cancellationToken);

        var lessonKinds = await GetLessonKindsAsync(allLessons.Select(l => l.Id), cancellationToken);

        return allLessons.Select(lesson =>
        {
            lessonProgressByLessonId.TryGetValue(lesson.Id, out var progressRecord);
            return new LessonSummaryDto(
                lesson.Id,
                lesson.Title,
                lesson.OrderInTopic,
                progressRecord?.Status ?? "locked",
                progressRecord?.BestScore ?? 0,
                lessonKinds.GetValueOrDefault(lesson.Id, "practice"));
        }).ToList();
    }

    /// <summary>
    /// Returns the "kind" of each lesson: "theory" when the lesson has at least one
    /// exercise and every exercise is a theory_card, otherwise "practice".
    /// </summary>
    private async Task<Dictionary<Guid, string>> GetLessonKindsAsync(
        IEnumerable<Guid> lessonIds,
        CancellationToken cancellationToken)
    {
        var ids = lessonIds.Distinct().ToList();
        if (ids.Count == 0) return new Dictionary<Guid, string>();

        var exerciseTypesByLesson = await databaseContext.Exercises
            .Where(exercise => ids.Contains(exercise.LessonId))
            .Select(exercise => new { exercise.LessonId, exercise.Type })
            .ToListAsync(cancellationToken);

        return exerciseTypesByLesson
            .GroupBy(exercise => exercise.LessonId)
            .ToDictionary(
                group => group.Key,
                group => group.All(exercise => exercise.Type == ExerciseTypes.TheoryCard) ? "theory" : "practice");
    }

    public async Task<IReadOnlyList<LessonSummaryDto>> GetLessonsForTopicAsync(
        Guid userId,
        Guid topicId,
        CancellationToken cancellationToken = default)
    {
        var lessonProgressByLessonId = await databaseContext.UserLessonProgressRecords
            .Where(progressRecord => progressRecord.UserId == userId)
            .ToDictionaryAsync(progressRecord => progressRecord.LessonId, cancellationToken);

        var allLessons = await databaseContext.Lessons
            .Where(lesson => lesson.TopicId == topicId)
            .OrderBy(lesson => lesson.OrderInTopic)
            .ToListAsync(cancellationToken);

        var lessonKinds = await GetLessonKindsAsync(allLessons.Select(l => l.Id), cancellationToken);

        return allLessons.Select(lesson =>
        {
            lessonProgressByLessonId.TryGetValue(lesson.Id, out var progressRecord);
            return new LessonSummaryDto(
                lesson.Id,
                lesson.Title,
                lesson.OrderInTopic,
                progressRecord?.Status ?? "locked",
                progressRecord?.BestScore ?? 0,
                lessonKinds.GetValueOrDefault(lesson.Id, "practice"));
        }).ToList();
    }

    public async Task<IReadOnlyList<LessonSummaryDto>> GetLessonsForSkillAsync(
        Guid userId,
        string skillSlug,
        CancellationToken cancellationToken = default)
    {
        // Find skill by iconic name (slug)
        var skill = await databaseContext.Skills
            .FirstOrDefaultAsync(s => s.IconicName == skillSlug, cancellationToken);

        if (skill is null)
            return [];

        // Get all topics for this skill
        var topicIds = await databaseContext.Topics
            .Where(t => t.SkillId == skill.Id)
            .Select(t => t.Id)
            .ToListAsync(cancellationToken);

        if (topicIds.Count == 0)
            return [];

        var lessonProgressByLessonId = await databaseContext.UserLessonProgressRecords
            .Where(progressRecord => progressRecord.UserId == userId)
            .ToDictionaryAsync(progressRecord => progressRecord.LessonId, cancellationToken);

        var allLessons = await databaseContext.Lessons
            .Where(lesson => topicIds.Contains(lesson.TopicId))
            .OrderBy(lesson => lesson.OrderInTopic)
            .ToListAsync(cancellationToken);

        var lessonKinds = await GetLessonKindsAsync(allLessons.Select(l => l.Id), cancellationToken);

        // Determine status for first lesson - should be available if not completed
        var isFirstLesson = true;

        return allLessons.Select(lesson =>
        {
            lessonProgressByLessonId.TryGetValue(lesson.Id, out var progressRecord);
            var status = progressRecord?.Status ?? (isFirstLesson ? "available" : "locked");
            isFirstLesson = false;
            return new LessonSummaryDto(
                lesson.Id,
                lesson.Title,
                lesson.OrderInTopic,
                status,
                progressRecord?.BestScore ?? 0,
                lessonKinds.GetValueOrDefault(lesson.Id, "practice"));
        }).ToList();
    }

    public async Task<IReadOnlyList<ExerciseDto>> GetExercisesForLessonAsync(
        Guid lessonId,
        CancellationToken cancellationToken = default)
    {
        var rawExercises = await databaseContext.Exercises
            .Where(exercise => exercise.LessonId == lessonId)
            .OrderBy(exercise => exercise.OrderInLesson)
            .Select(exercise => new { exercise.Id, exercise.Type, exercise.OrderInLesson, exercise.SerializedContent })
            .ToListAsync(cancellationToken);

        return rawExercises.Select(rawExercise => new ExerciseDto(
            rawExercise.Id,
            rawExercise.Type,
            rawExercise.OrderInLesson,
            JsonDocument.Parse(rawExercise.SerializedContent).RootElement))
            .ToList();
    }

    public async Task<ExerciseSubmissionResultDto> SubmitExerciseAnswerAsync(
        Guid userId,
        Guid exerciseId,
        JsonElement userAnswer,
        CancellationToken cancellationToken = default)
    {
        var exercise = await databaseContext.Exercises
            .FirstOrDefaultAsync(exerciseRecord => exerciseRecord.Id == exerciseId, cancellationToken)
            ?? throw new KeyNotFoundException($"Exercise {exerciseId} not found.");

        var evaluationStrategy = evaluationFactory.GetStrategyForExerciseType(exercise.Type);
        var exerciseContent = JsonDocument.Parse(exercise.SerializedContent).RootElement;

        var evaluationResult = await evaluationStrategy.EvaluateAnswerAsync(
            exerciseContent, userAnswer, cancellationToken);

        var newAttempt = new UserExerciseAttempt
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ExerciseId = exerciseId,
            SerializedAnswer = userAnswer.GetRawText(),
            IsCorrect = evaluationResult.IsCorrect,
            Score = evaluationResult.Score,
            SerializedAiFeedback = evaluationResult.AiFeedback is not null
                ? JsonSerializer.Serialize(new { feedback = evaluationResult.AiFeedback })
                : null,
            AttemptedAt = DateTime.UtcNow
        };

        databaseContext.UserExerciseAttempts.Add(newAttempt);

        var experiencePointsEarned = 0;
        if (evaluationResult.IsCorrect)
        {
            // Base XP per exercise type is admin-editable in the database (ExerciseTypeRewards).
            experiencePointsEarned = await gamificationService.GetExerciseBaseXpAsync(exercise.Type, cancellationToken);

            databaseContext.UserXpRecords.Add(new UserXp
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Amount = experiencePointsEarned,
                Source = "exercise",
                EarnedAt = DateTime.UtcNow
            });

            await UpdateLessonProgressAsync(userId, exercise.LessonId, cancellationToken);
            await UpdateStreakForUserAsync(userId, cancellationToken);
        }

        IReadOnlyList<string> newlyUnlockedAchievementKeys = Array.Empty<string>();
        if (evaluationResult.IsCorrect)
            newlyUnlockedAchievementKeys = await achievementService.EvaluateAchievementsAfterSubmitAsync(userId, cancellationToken);

        await databaseContext.SaveChangesAsync(cancellationToken);

        return new ExerciseSubmissionResultDto(
            evaluationResult.IsCorrect,
            evaluationResult.Score,
            evaluationResult.Explanation,
            evaluationResult.AiFeedback,
            experiencePointsEarned,
            newlyUnlockedAchievementKeys);
    }

    private async Task UpdateLessonProgressAsync(
        Guid userId,
        Guid lessonId,
        CancellationToken cancellationToken = default)
    {
        var progressRecord = await databaseContext.UserLessonProgressRecords
            .FirstOrDefaultAsync(record => record.UserId == userId && record.LessonId == lessonId, cancellationToken);

        if (progressRecord is null)
        {
            progressRecord = new UserLessonProgress
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                LessonId = lessonId,
                Status = "completed",
                BestScore = 100,
                CompletedAt = DateTime.UtcNow
            };
            databaseContext.UserLessonProgressRecords.Add(progressRecord);
        }
        else if (progressRecord.Status != "completed")
        {
            progressRecord.Status = "completed";
            progressRecord.BestScore = 100;
            progressRecord.CompletedAt = DateTime.UtcNow;
        }

        var lesson = await databaseContext.Lessons
            .FirstOrDefaultAsync(lessonRecord => lessonRecord.Id == lessonId, cancellationToken);

        if (lesson is null) return;

        await UnlockNextLessonInTopicAsync(userId, lesson, cancellationToken);
    }

    private async Task UnlockNextLessonInTopicAsync(
        Guid userId,
        Lesson completedLesson,
        CancellationToken cancellationToken = default)
    {
        var nextLesson = await databaseContext.Lessons
            .Where(lesson => lesson.TopicId == completedLesson.TopicId
                        && lesson.OrderInTopic > completedLesson.OrderInTopic)
            .OrderBy(lesson => lesson.OrderInTopic)
            .ThenBy(lesson => lesson.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (nextLesson is null) return;

        var nextProgress = await databaseContext.UserLessonProgressRecords
            .FirstOrDefaultAsync(record => record.UserId == userId && record.LessonId == nextLesson.Id, cancellationToken);

        if (nextProgress is null)
        {
            databaseContext.UserLessonProgressRecords.Add(new UserLessonProgress
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                LessonId = nextLesson.Id,
                Status = "available"
            });
        }
        else if (nextProgress.Status == "locked")
        {
            nextProgress.Status = "available";
        }
    }

    private async Task UpdateStreakForUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var streakRecord = await databaseContext.UserStreaks
            .FirstOrDefaultAsync(streak => streak.UserId == userId, cancellationToken);

        if (streakRecord is null)
        {
            databaseContext.UserStreaks.Add(new UserStreak
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                CurrentStreakDayCount = 1,
                LongestStreakDayCount = 1,
                LastActivityDate = today
            });
            await AwardStreakBonusExperiencePointsIfMilestoneAsync(userId, 1, cancellationToken);
            return;
        }

        if (streakRecord.LastActivityDate == today) return;

        var isConsecutiveDay = streakRecord.LastActivityDate == today.AddDays(-1);
        streakRecord.CurrentStreakDayCount = isConsecutiveDay
            ? streakRecord.CurrentStreakDayCount + 1
            : 1;

        if (streakRecord.CurrentStreakDayCount > streakRecord.LongestStreakDayCount)
            streakRecord.LongestStreakDayCount = streakRecord.CurrentStreakDayCount;

        streakRecord.LastActivityDate = today;

        await AwardStreakBonusExperiencePointsIfMilestoneAsync(
            userId,
            streakRecord.CurrentStreakDayCount,
            cancellationToken);
    }

    private async Task AwardStreakBonusExperiencePointsIfMilestoneAsync(
        Guid userId,
        int currentStreak,
        CancellationToken cancellationToken)
    {
        // Streak milestone bonuses are admin-editable in the database (StreakMilestones).
        int bonusExperiencePoints = await gamificationService.GetStreakBonusXpAsync(currentStreak, cancellationToken);

        if (bonusExperiencePoints == 0) return;

        databaseContext.UserXpRecords.Add(new UserXp
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Amount = bonusExperiencePoints,
            Source = "streak_bonus",
            EarnedAt = DateTime.UtcNow
        });

        await notificationService.CreateAsync(
            recipientUserId: userId,
            notificationType: NotificationType.StreakMilestone,
            title: $"🔥 Стрик {currentStreak} дней!",
            body: $"Вы получили бонус +{bonusExperiencePoints} XP за серию из {currentStreak} дней подряд.",
            actionUrl: "/profile",
            relatedEntityId: currentStreak.ToString(),
            cancellationToken: cancellationToken);
    }

    public async Task<ExerciseChatResponseDto> SendChatMessageAsync(
        Guid userId,
        Guid exerciseId,
        string userMessage,
        CancellationToken cancellationToken = default)
    {
        var chatContext = await BuildExerciseChatContextAsync(exerciseId, cancellationToken);
        var cacheKey = BuildChatCacheKey(userId, exerciseId);
        var messages = await GetChatMessagesFromCacheAsync(cacheKey, cancellationToken);

        // The user always speaks first (e.g. opens the cold call). With no user
        // message yet there is nothing to respond to — return an empty turn.
        if (string.IsNullOrWhiteSpace(userMessage))
        {
            return new ExerciseChatResponseDto(
                Response: string.Empty,
                IsComplete: false,
                IsFinished: false,
                TurnNumber: messages.Count(m => m.Role == "user"),
                MaxTurns: chatContext.MaxTurns);
        }

        messages.Add(new ChatMessage("user", userMessage));

        var turnNumber = messages.Count(m => m.Role == "user");
        if (turnNumber > chatContext.MaxTurns)
        {
            await SaveChatMessagesToCacheAsync(cacheKey, messages, cancellationToken);
            return new ExerciseChatResponseDto(
                Response: "Диалог завершён — достигнуто максимальное количество реплик.",
                IsComplete: true,
                IsFinished: false,
                TurnNumber: turnNumber,
                MaxTurns: chatContext.MaxTurns);
        }

        var dialogHistory = ToDialogHistory(messages);

        var aiResponse = await GenerateAiResponseAsync(chatContext.SystemPrompt, dialogHistory, cancellationToken);
        messages.Add(new ChatMessage("assistant", aiResponse.Response));

        await SaveChatMessagesToCacheAsync(cacheKey, messages, cancellationToken);

        return new ExerciseChatResponseDto(
            Response: aiResponse.Response,
            IsComplete: aiResponse.IsComplete || turnNumber >= chatContext.MaxTurns,
            IsFinished: aiResponse.IsFinished,
            TurnNumber: turnNumber,
            MaxTurns: chatContext.MaxTurns);
    }

    public async IAsyncEnumerable<VoiceStreamChunk> StreamExerciseVoiceAsync(
        Guid userId,
        Guid exerciseId,
        string transcript,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var chatContext = await BuildExerciseChatContextAsync(exerciseId, cancellationToken);
        var cacheKey = BuildChatCacheKey(userId, exerciseId);
        var messages = await GetChatMessagesFromCacheAsync(cacheKey, cancellationToken);

        if (!string.IsNullOrWhiteSpace(transcript))
            messages.Add(new ChatMessage("user", transcript));

        var dialogHistory = ToDialogHistory(messages);

        // Same pipeline as live calls: stream the JSON reply, chunk into sentences,
        // synthesize each sentence to speech while the next one is still streaming.
        var replyParser = new StreamingChatReplyParser();
        var sentenceChunker = new SentenceChunker();
        var pendingAudio = new Queue<Task<byte[]?>>();

        await foreach (var delta in openAiChatService.StreamChatMessageAsync(chatContext.SystemPrompt, dialogHistory, cancellationToken))
        {
            var replyText = replyParser.Push(delta);
            if (replyText.Length == 0) continue;

            sentenceChunker.Append(replyText);

            while (sentenceChunker.TryExtractSentence(out var sentence))
            {
                var cleaned = sentence.Trim();
                if (string.IsNullOrWhiteSpace(cleaned)) continue;

                yield return new VoiceStreamChunk(cleaned, Array.Empty<byte>(), IsStopSignal: false, IsFinal: false);
                pendingAudio.Enqueue(TrySynthesizeAsync(cleaned, cancellationToken));
            }

            while (pendingAudio.Count > 0 && pendingAudio.Peek().IsCompleted)
            {
                var readyAudio = await pendingAudio.Dequeue();
                if (readyAudio is { Length: > 0 })
                    yield return new VoiceStreamChunk(string.Empty, readyAudio, IsStopSignal: false, IsFinal: false);
            }
        }

        var parseResult = replyParser.Complete();
        if (parseResult.UsedFallback)
            sentenceChunker.Replace(parseResult.Reply);

        var tailCleaned = sentenceChunker.DrainRemaining().Trim();
        if (!string.IsNullOrWhiteSpace(tailCleaned))
        {
            yield return new VoiceStreamChunk(tailCleaned, Array.Empty<byte>(), IsStopSignal: parseResult.EndCall, IsFinal: false);
            pendingAudio.Enqueue(TrySynthesizeAsync(tailCleaned, cancellationToken));
        }

        while (pendingAudio.Count > 0)
        {
            var audio = await pendingAudio.Dequeue();
            if (audio is { Length: > 0 })
                yield return new VoiceStreamChunk(string.Empty, audio, IsStopSignal: false, IsFinal: false);
        }

        messages.Add(new ChatMessage("assistant", parseResult.Reply));
        await SaveChatMessagesToCacheAsync(cacheKey, messages, cancellationToken);

        var maxTurnsReached = messages.Count(m => m.Role == "user") >= chatContext.MaxTurns;
        yield return new VoiceStreamChunk(
            string.Empty,
            Array.Empty<byte>(),
            IsStopSignal: parseResult.EndCall || maxTurnsReached,
            IsFinal: true);
    }

    private async Task<byte[]?> TrySynthesizeAsync(string text, CancellationToken cancellationToken)
    {
        try
        {
            var stream = await ttsRouter.SynthesizeSpeechAsync(text, null, cancellationToken);
            await using (stream)
            {
                using var memoryStream = new MemoryStream();
                await stream.CopyToAsync(memoryStream, cancellationToken);
                return memoryStream.ToArray();
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Exercise TTS synthesis failed ({TextLength} chars); reply delivered as text only", text.Length);
            return null;
        }
    }

    private async Task<ExerciseChatContext> BuildExerciseChatContextAsync(
        Guid exerciseId,
        CancellationToken cancellationToken)
    {
        var exercise = await databaseContext.Exercises
            .FirstOrDefaultAsync(e => e.Id == exerciseId, cancellationToken)
            ?? throw new KeyNotFoundException($"Exercise {exerciseId} not found.");

        if (exercise.Type != ExerciseTypes.AiDialogue)
            throw new NotSupportedException("Chat is only supported for ai_dialogue exercises.");

        var content = JsonDocument.Parse(exercise.SerializedContent).RootElement;
        var maxTurns = content.TryGetProperty("max_turns", out var maxEl) ? maxEl.GetInt32() : 10;

        var persona = content.TryGetProperty("persona", out var personaEl) ? personaEl.GetString() ?? "" : "";
        var scenario = content.TryGetProperty("scenario", out var scenarioEl) ? scenarioEl.GetString() ?? "" : "";
        var contextInfo = content.TryGetProperty("context", out var contextEl) ? contextEl.GetString() ?? "" : "";
        var aiPrompt = content.TryGetProperty("ai_prompt", out var aiPromptEl) ? aiPromptEl.GetString() ?? "" : "";

        var systemPrompt = !string.IsNullOrEmpty(aiPrompt)
            ? aiPrompt
            : $"Ты играешь роль: {persona}. Сценарий: {scenario}. {contextInfo}\n\nОтвечай кратко, в 1-3 предложения. Веди себя естественно для своей роли. Пользователь звонит первым.";

        return new ExerciseChatContext(systemPrompt, maxTurns);
    }

    private static string BuildChatCacheKey(Guid userId, Guid exerciseId) =>
        $"exercise_chat:{userId}:{exerciseId}";

    private static List<DialogMessage> ToDialogHistory(IEnumerable<ChatMessage> messages) =>
        messages.Select(m => new DialogMessage
        {
            Role = m.Role,
            Content = m.Content,
            Timestamp = DateTime.UtcNow
        }).ToList();

    private sealed record ExerciseChatContext(string SystemPrompt, int MaxTurns);

    private record ChatMessage(string Role, string Content);

    private record AiChatResponse(string Response, bool IsComplete, bool IsFinished);

    private Task<List<ChatMessage>> GetChatMessagesFromCacheAsync(string cacheKey, CancellationToken cancellationToken)
    {
        // For now, use in-memory storage via static dictionary
        // In production, this should use Redis
        if (_chatCache.TryGetValue(cacheKey, out var messages))
            return Task.FromResult(messages);
        return Task.FromResult(new List<ChatMessage>());
    }

    private Task SaveChatMessagesToCacheAsync(string cacheKey, List<ChatMessage> messages, CancellationToken cancellationToken)
    {
        _chatCache[cacheKey] = messages;
        return Task.CompletedTask;
    }

    private static readonly Dictionary<string, List<ChatMessage>> _chatCache = new();

    private async Task<AiChatResponse> GenerateAiResponseAsync(
        string systemPrompt,
        List<DialogMessage> messages,
        CancellationToken cancellationToken)
    {
        if (!openAiChatService.IsConfigured)
        {
            logger.LogWarning("OpenAI service is not configured, using fallback response");
            // Fallback for when AI is not configured
            var isComplete = messages.Count(m => m.Role == "user") >= 3 &&
                             messages.LastOrDefault()?.Content.Contains("спасибо", StringComparison.OrdinalIgnoreCase) == true;

            return new AiChatResponse(
                Response: "Понял вас. Что ещё вы хотели бы обсудить?",
                IsComplete: isComplete,
                IsFinished: false);
        }

        try
        {
            var result = await openAiChatService.SendChatMessageAsync(systemPrompt, messages, cancellationToken);
            return new AiChatResponse(
                Response: result.Content,
                IsComplete: result.IsStopSignal,
                IsFinished: result.IsStopSignal);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to generate AI response for chat, using fallback");
            // Fallback on error
            return new AiChatResponse(
                Response: "Понял вас. Что ещё вы хотели бы обсудить?",
                IsComplete: false,
                IsFinished: false);
        }
    }
}
