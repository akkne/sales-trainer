using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Sellevate.Learning.Common.Constants;
using Sellevate.Learning.Eventing;
using Sellevate.Learning.Features.Exercises.Models;
using Sellevate.Learning.Features.Exercises.Services.Abstract;
using Sellevate.Learning.Features.Lessons.Models;
using Sellevate.Learning.Infrastructure.Ai;
using Sellevate.Learning.Infrastructure.Data;

namespace Sellevate.Learning.Features.Exercises.Services.Implementation;

internal sealed class ExerciseService(
    LearningDbContext databaseContext,
    ExerciseEvaluationFactory evaluationFactory,
    ILearningEventPublisher eventPublisher,
    IExerciseDialogService exerciseDialogService) : IExerciseService
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

        var lessonKinds = await GetLessonKindsAsync(allLessons.Select(lesson => lesson.Id), cancellationToken);

        return allLessons.Select(lesson =>
        {
            lessonProgressByLessonId.TryGetValue(lesson.Id, out var progressRecord);
            return new LessonSummaryDto(
                lesson.Id,
                lesson.Title,
                lesson.OrderInTopic,
                progressRecord?.Status ?? LessonProgressStatuses.Locked,
                progressRecord?.BestScore ?? 0,
                lessonKinds.GetValueOrDefault(lesson.Id, LessonKinds.Practice));
        }).ToList();
    }

    private async Task<Dictionary<Guid, string>> GetLessonKindsAsync(
        IEnumerable<Guid> lessonIds,
        CancellationToken cancellationToken)
    {
        var distinctLessonIds = lessonIds.Distinct().ToList();
        if (distinctLessonIds.Count == 0) return new Dictionary<Guid, string>();

        var exerciseTypesByLesson = await databaseContext.Exercises
            .Where(exercise => distinctLessonIds.Contains(exercise.LessonId))
            .Select(exercise => new { exercise.LessonId, exercise.Type })
            .ToListAsync(cancellationToken);

        return exerciseTypesByLesson
            .GroupBy(exercise => exercise.LessonId)
            .ToDictionary(
                group => group.Key,
                group => group.All(exercise => exercise.Type == ExerciseTypes.TheoryCard)
                    ? LessonKinds.Theory
                    : LessonKinds.Practice);
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

        var lessonKinds = await GetLessonKindsAsync(allLessons.Select(lesson => lesson.Id), cancellationToken);

        return allLessons.Select(lesson =>
        {
            lessonProgressByLessonId.TryGetValue(lesson.Id, out var progressRecord);
            return new LessonSummaryDto(
                lesson.Id,
                lesson.Title,
                lesson.OrderInTopic,
                progressRecord?.Status ?? LessonProgressStatuses.Locked,
                progressRecord?.BestScore ?? 0,
                lessonKinds.GetValueOrDefault(lesson.Id, LessonKinds.Practice));
        }).ToList();
    }

    public async Task<IReadOnlyList<LessonSummaryDto>> GetLessonsForSkillAsync(
        Guid userId,
        string skillSlug,
        CancellationToken cancellationToken = default)
    {
        var skill = await databaseContext.Skills
            .FirstOrDefaultAsync(candidate => candidate.IconicName == skillSlug, cancellationToken);

        if (skill is null)
            return [];

        var topicIds = await databaseContext.Topics
            .Where(topic => topic.SkillId == skill.Id)
            .Select(topic => topic.Id)
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

        var lessonKinds = await GetLessonKindsAsync(allLessons.Select(lesson => lesson.Id), cancellationToken);

        var isFirstLesson = true;

        return allLessons.Select(lesson =>
        {
            lessonProgressByLessonId.TryGetValue(lesson.Id, out var progressRecord);
            var status = progressRecord?.Status
                ?? (isFirstLesson ? LessonProgressStatuses.Available : LessonProgressStatuses.Locked);
            isFirstLesson = false;
            return new LessonSummaryDto(
                lesson.Id,
                lesson.Title,
                lesson.OrderInTopic,
                status,
                progressRecord?.BestScore ?? 0,
                lessonKinds.GetValueOrDefault(lesson.Id, LessonKinds.Practice));
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

        return rawExercises.Select(rawExercise =>
        {
            var fullContent = JsonDocument.Parse(rawExercise.SerializedContent).RootElement;
            var learnerContent = StripAnswerKeyFields(rawExercise.Type, fullContent);
            return new ExerciseDto(
                rawExercise.Id,
                rawExercise.Type,
                rawExercise.OrderInLesson,
                learnerContent);
        }).ToList();
    }

    /// <summary>
    /// Removes answer-key fields (is_correct, correct_position, is_mistake, category, ai_prompt)
    /// from exercise content before sending it to learners, so they cannot trivially cheat.
    /// </summary>
    private static JsonElement StripAnswerKeyFields(string exerciseType, JsonElement content)
    {
        // Fields to strip at item/option level, keyed by exercise type.
        // Top-level fields to remove are handled separately.
        string[]? itemArrayProperty = null;
        string[]? itemFieldsToStrip = null;
        string[]? topLevelFieldsToStrip = null;

        switch (exerciseType)
        {
            case ExerciseTypes.ChooseOption:
            case ExerciseTypes.FillBlank:
                itemArrayProperty = ["options"];
                itemFieldsToStrip = ["is_correct"];
                break;
            case ExerciseTypes.Reorder:
                itemArrayProperty = ["items"];
                itemFieldsToStrip = ["correct_position"];
                break;
            case ExerciseTypes.Categorize:
                itemArrayProperty = ["items"];
                itemFieldsToStrip = ["category"];
                break;
            case ExerciseTypes.SpotMistake:
                itemArrayProperty = ["dialogue"];
                itemFieldsToStrip = ["is_mistake"];
                break;
            case ExerciseTypes.AiDialogue:
                topLevelFieldsToStrip = ["ai_prompt"];
                break;
            default:
                // No answer-key fields for theory_card, match_pairs, rewrite, free_text, evaluate_call.
                return content;
        }

        // Rebuild as a plain dictionary so we can mutate freely.
        var dict = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var prop in content.EnumerateObject())
            dict[prop.Name] = prop.Value;

        // Strip top-level fields.
        if (topLevelFieldsToStrip is not null)
        {
            foreach (var field in topLevelFieldsToStrip)
                dict.Remove(field);
        }

        // Strip per-item fields from a named array.
        if (itemArrayProperty is not null && itemFieldsToStrip is not null)
        {
            foreach (var arrayProp in itemArrayProperty)
            {
                if (!dict.TryGetValue(arrayProp, out var arrayEl) ||
                    arrayEl.ValueKind != JsonValueKind.Array)
                    continue;

                var strippedItems = new List<Dictionary<string, JsonElement>>();
                foreach (var item in arrayEl.EnumerateArray())
                {
                    var itemDict = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
                    foreach (var itemProp in item.EnumerateObject())
                    {
                        if (Array.IndexOf(itemFieldsToStrip, itemProp.Name) < 0)
                            itemDict[itemProp.Name] = itemProp.Value;
                    }
                    strippedItems.Add(itemDict);
                }

                // Re-serialise the sanitised array back into a JsonElement.
                var sanitisedJson = JsonSerializer.Serialize(new { array = strippedItems });
                var sanitisedDoc = JsonDocument.Parse(sanitisedJson);
                dict[arrayProp] = sanitisedDoc.RootElement.GetProperty("array");
            }
        }

        var resultJson = JsonSerializer.Serialize(dict);
        return JsonDocument.Parse(resultJson).RootElement;
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
        // Persist the attempt first so UpdateLessonProgressAsync can count it
        // when querying passed exercises (required for LE3 all-exercises-passed gate).
        await databaseContext.SaveChangesAsync(cancellationToken);

        var (lessonWasCompleted, lessonBestScore) = (false, 0);
        if (evaluationResult.IsCorrect)
        {
            (lessonWasCompleted, lessonBestScore) = await UpdateLessonProgressAsync(
                userId, exercise.LessonId, evaluationResult.Score, cancellationToken);
        }

        await databaseContext.SaveChangesAsync(cancellationToken);

        await eventPublisher.PublishExerciseCompletedAsync(
            new ExerciseCompletedEvent(userId, exercise.Type, evaluationResult.Score, evaluationResult.IsCorrect),
            cancellationToken);

        if (lessonWasCompleted)
        {
            await eventPublisher.PublishLessonCompletedAsync(
                new LessonCompletedEvent(userId, exercise.LessonId, lessonBestScore), cancellationToken);

            await PublishSkillCompletionIfFinishedAsync(userId, exercise.LessonId, cancellationToken);
        }

        return new ExerciseSubmissionResultDto(
            evaluationResult.IsCorrect,
            evaluationResult.Score,
            evaluationResult.Explanation,
            evaluationResult.AiFeedback,
            XpEarned: 0,
            NewlyUnlockedAchievementKeys: Array.Empty<string>());
    }

    /// <summary>
    /// Updates lesson progress after a correct answer.
    /// Lesson is marked complete only when the user has at least one correct attempt
    /// for EVERY exercise in the lesson (all exercises passed).
    /// BestScore = max(existing best, current score).
    /// Returns (transitionedToCompleted, bestScore).
    /// </summary>
    private async Task<(bool TransitionedToCompleted, int BestScore)> UpdateLessonProgressAsync(
        Guid userId,
        Guid lessonId,
        int currentScore,
        CancellationToken cancellationToken = default)
    {
        // Count how many distinct exercises exist in this lesson.
        var totalExercises = await databaseContext.Exercises
            .Where(e => e.LessonId == lessonId)
            .CountAsync(cancellationToken);

        // Count how many distinct exercises the user has at least one correct attempt for.
        var passedExercises = await databaseContext.UserExerciseAttempts
            .Where(a => a.UserId == userId && a.IsCorrect)
            .Join(databaseContext.Exercises,
                attempt => attempt.ExerciseId,
                exercise => exercise.Id,
                (attempt, exercise) => exercise)
            .Where(e => e.LessonId == lessonId)
            .Select(e => e.Id)
            .Distinct()
            .CountAsync(cancellationToken);

        var allPassed = totalExercises > 0 && passedExercises >= totalExercises;

        var progressRecord = await databaseContext.UserLessonProgressRecords
            .FirstOrDefaultAsync(record => record.UserId == userId && record.LessonId == lessonId, cancellationToken);

        var transitionedToCompleted = false;
        int bestScore;

        if (progressRecord is null)
        {
            bestScore = currentScore;
            var newStatus = allPassed ? LessonProgressStatuses.Completed : LessonProgressStatuses.Available;
            progressRecord = new UserLessonProgress
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                LessonId = lessonId,
                Status = newStatus,
                BestScore = bestScore,
                CompletedAt = allPassed ? DateTime.UtcNow : null
            };
            databaseContext.UserLessonProgressRecords.Add(progressRecord);
            transitionedToCompleted = allPassed;
        }
        else
        {
            bestScore = Math.Max(progressRecord.BestScore, currentScore);
            progressRecord.BestScore = bestScore;

            if (allPassed && progressRecord.Status != LessonProgressStatuses.Completed)
            {
                progressRecord.Status = LessonProgressStatuses.Completed;
                progressRecord.CompletedAt = DateTime.UtcNow;
                transitionedToCompleted = true;
            }
        }

        if (transitionedToCompleted)
        {
            var lesson = await databaseContext.Lessons
                .FirstOrDefaultAsync(lessonRecord => lessonRecord.Id == lessonId, cancellationToken);

            if (lesson is not null)
            {
                await UnlockNextLessonInTopicAsync(userId, lesson, cancellationToken);
            }
        }

        return (transitionedToCompleted, bestScore);
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
                Status = LessonProgressStatuses.Available
            });
        }
        else if (nextProgress.Status == LessonProgressStatuses.Locked)
        {
            nextProgress.Status = LessonProgressStatuses.Available;
        }
    }

    private async Task PublishSkillCompletionIfFinishedAsync(
        Guid userId,
        Guid lessonId,
        CancellationToken cancellationToken)
    {
        var skillId = await databaseContext.Lessons
            .Where(lesson => lesson.Id == lessonId)
            .Join(databaseContext.Topics,
                lesson => lesson.TopicId,
                topic => topic.Id,
                (lesson, topic) => (Guid?)topic.SkillId)
            .FirstOrDefaultAsync(cancellationToken);

        if (skillId is null) return;

        var topicIds = await databaseContext.Topics
            .Where(topic => topic.SkillId == skillId.Value)
            .Select(topic => topic.Id)
            .ToListAsync(cancellationToken);

        var totalLessonCount = await databaseContext.Lessons
            .Where(lesson => topicIds.Contains(lesson.TopicId))
            .CountAsync(cancellationToken);

        if (totalLessonCount == 0) return;

        var completedLessonCount = await databaseContext.UserLessonProgressRecords
            .Where(progress => progress.UserId == userId && progress.Status == LessonProgressStatuses.Completed)
            .Join(databaseContext.Lessons,
                progress => progress.LessonId,
                lesson => lesson.Id,
                (progress, lesson) => lesson)
            .Where(lesson => topicIds.Contains(lesson.TopicId))
            .CountAsync(cancellationToken);

        if (completedLessonCount >= totalLessonCount)
        {
            await eventPublisher.PublishSkillCompletedAsync(
                new SkillCompletedEvent(userId, skillId.Value), cancellationToken);
        }
    }

    public Task<ExerciseChatResponseDto> SendChatMessageAsync(
        Guid userId,
        Guid exerciseId,
        string userMessage,
        CancellationToken cancellationToken = default) =>
        exerciseDialogService.SendChatMessageAsync(userId, exerciseId, userMessage, cancellationToken);

    public IAsyncEnumerable<VoiceStreamChunk> StreamExerciseVoiceAsync(
        Guid userId,
        Guid exerciseId,
        string transcript,
        CancellationToken cancellationToken = default) =>
        exerciseDialogService.StreamExerciseVoiceAsync(userId, exerciseId, transcript, cancellationToken);

    public Task ValidateExerciseForVoiceAsync(Guid exerciseId, CancellationToken cancellationToken = default) =>
        exerciseDialogService.ValidateExerciseForVoiceAsync(exerciseId, cancellationToken);
}
