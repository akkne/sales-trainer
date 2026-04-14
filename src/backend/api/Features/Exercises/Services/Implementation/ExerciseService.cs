using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SalesTrainer.Api.Features.Achievements.Services.Abstract;
using SalesTrainer.Api.Features.Exercises.Models;
using SalesTrainer.Api.Features.Exercises.Services.Abstract;
using SalesTrainer.Api.Features.Gamification.Models;
using SalesTrainer.Api.Features.Lessons.Models;
using SalesTrainer.Api.Infrastructure.Data;

namespace SalesTrainer.Api.Features.Exercises.Services.Implementation;

internal sealed class ExerciseService(
    AppDbContext databaseContext,
    ExerciseEvaluationFactory evaluationFactory,
    IAchievementService achievementService) : IExerciseService
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

        return allLessons.Select(lesson =>
        {
            lessonProgressByLessonId.TryGetValue(lesson.Id, out var progressRecord);
            return new LessonSummaryDto(
                lesson.Id,
                lesson.Title,
                lesson.OrderInTopic,
                progressRecord?.Status ?? "locked",
                progressRecord?.BestScore ?? 0);
        }).ToList();
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

        return allLessons.Select(lesson =>
        {
            lessonProgressByLessonId.TryGetValue(lesson.Id, out var progressRecord);
            return new LessonSummaryDto(
                lesson.Id,
                lesson.Title,
                lesson.OrderInTopic,
                progressRecord?.Status ?? "locked",
                progressRecord?.BestScore ?? 0);
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
            experiencePointsEarned = 10; // Fixed XP per exercise

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
            AwardStreakBonusExperiencePointsIfMilestone(userId, 1);
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

        AwardStreakBonusExperiencePointsIfMilestone(userId, streakRecord.CurrentStreakDayCount);
    }

    private void AwardStreakBonusExperiencePointsIfMilestone(Guid userId, int currentStreak)
    {
        int bonusExperiencePoints = currentStreak switch
        {
            7  => 50,
            30 => 200,
            _  => 0
        };

        if (bonusExperiencePoints == 0) return;

        databaseContext.UserXpRecords.Add(new UserXp
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Amount = bonusExperiencePoints,
            Source = "streak_bonus",
            EarnedAt = DateTime.UtcNow
        });
    }

    public async Task<ExerciseChatResponseDto> SendChatMessageAsync(
        Guid userId,
        Guid exerciseId,
        string userMessage,
        CancellationToken cancellationToken = default)
    {
        var exercise = await databaseContext.Exercises
            .FirstOrDefaultAsync(e => e.Id == exerciseId, cancellationToken)
            ?? throw new KeyNotFoundException($"Exercise {exerciseId} not found.");

        if (exercise.Type != "ai_dialog")
            throw new NotSupportedException("Chat is only supported for ai_dialog exercises.");

        var content = JsonDocument.Parse(exercise.SerializedContent).RootElement;
        var maxTurns = content.TryGetProperty("maxTurns", out var maxEl) ? maxEl.GetInt32() : 10;
        var chatSystemPrompt = content.GetProperty("chatSystemPrompt").GetString() ?? "";

        // Get or create chat state from cache (using Redis pattern key)
        var cacheKey = $"exercise_chat:{userId}:{exerciseId}";
        var messages = await GetChatMessagesFromCacheAsync(cacheKey, cancellationToken);

        // If no messages, start with AI greeting
        if (messages.Count == 0)
        {
            var greeting = await GenerateAiResponseAsync(chatSystemPrompt, messages, cancellationToken);
            messages.Add(new ChatMessage("assistant", greeting.Response));
            await SaveChatMessagesToCacheAsync(cacheKey, messages, cancellationToken);

            return new ExerciseChatResponseDto(
                Response: greeting.Response,
                IsComplete: greeting.IsComplete,
                TurnNumber: 1,
                MaxTurns: maxTurns);
        }

        // Add user message
        messages.Add(new ChatMessage("user", userMessage));

        var turnNumber = messages.Count(m => m.Role == "user");
        if (turnNumber >= maxTurns)
        {
            await SaveChatMessagesToCacheAsync(cacheKey, messages, cancellationToken);
            return new ExerciseChatResponseDto(
                Response: "Диалог завершён — достигнуто максимальное количество реплик.",
                IsComplete: true,
                TurnNumber: turnNumber,
                MaxTurns: maxTurns);
        }

        // Generate AI response
        var aiResponse = await GenerateAiResponseAsync(chatSystemPrompt, messages, cancellationToken);
        messages.Add(new ChatMessage("assistant", aiResponse.Response));

        await SaveChatMessagesToCacheAsync(cacheKey, messages, cancellationToken);

        return new ExerciseChatResponseDto(
            Response: aiResponse.Response,
            IsComplete: aiResponse.IsComplete,
            TurnNumber: turnNumber,
            MaxTurns: maxTurns);
    }

    private record ChatMessage(string Role, string Content);

    private record AiChatResponse(string Response, bool IsComplete);

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
        List<ChatMessage> messages,
        CancellationToken cancellationToken)
    {
        // This is a simplified implementation - in production should use OpenAI service
        // For now, return a placeholder that indicates the chat endpoint works
        await Task.CompletedTask;

        // TODO: Implement actual OpenAI call similar to DialogService
        // For now, return placeholder
        var isComplete = messages.Count(m => m.Role == "user") >= 3 &&
                         messages.LastOrDefault()?.Content.Contains("спасибо", StringComparison.OrdinalIgnoreCase) == true;

        return new AiChatResponse(
            Response: "Понял вас. Что ещё вы хотели бы обсудить?",
            IsComplete: isComplete);
    }
}
