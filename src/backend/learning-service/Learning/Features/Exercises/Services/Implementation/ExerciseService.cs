using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Sellevate.BuildingBlocks.ContentTemplating;
using Sellevate.Learning.Common.Constants;
using Sellevate.Learning.Eventing;
using Sellevate.Learning.Features.Content;
using Sellevate.Learning.Features.Content.Services.Abstract;
using Sellevate.Learning.Features.Exercises.Models;
using Sellevate.Learning.Features.Exercises.Services.Abstract;
using Sellevate.Learning.Features.Lessons.Models;
using Sellevate.Learning.Features.Lessons.Services.Abstract;
using Sellevate.Learning.Infrastructure.Ai;
using Sellevate.Learning.Infrastructure.Data;

namespace Sellevate.Learning.Features.Exercises.Services.Implementation;

internal sealed class ExerciseService(
    LearningDbContext databaseContext,
    ExerciseEvaluationFactory evaluationFactory,
    ILearningEventPublisher eventPublisher,
    IExerciseDialogService exerciseDialogService,
    ILessonVersionService lessonVersionService,
    IOrganizationProfileProvider organizationProfileProvider,
    ILogger<ExerciseService> logger) : IExerciseService
{
    /// <summary>
    /// Phase 40.19. Reports <c>{{organization.*}}</c> placeholders this service could not resolve.
    ///
    /// <para>
    /// <b>Where rendering happens is the whole reason the substitution is safe.</b> The rows and the
    /// 40.15 snapshot both keep the template; only the response carries the rendered text. If it were
    /// the other way round, publishing the same base lesson in two organizations would produce two
    /// different <c>ContentHash</c> values and the shared library would silently fork per customer —
    /// the expensive path 40.18 exists to make rare.
    /// </para>
    /// </summary>
    private void LogUnresolved(List<string> unresolved)
    {
        if (unresolved.Count > 0)
        {
            // Warning, not an exception: the learner sees a sentence with a word missing, which is a
            // content bug to fix, not a reason to fail the lesson they are in the middle of.
            logger.LogWarning(
                "Unresolved organization placeholders in learning content: {Placeholders}",
                string.Join(", ", unresolved.Distinct()));
        }
    }

    public async Task<IReadOnlyList<LessonSummaryDto>> GetAllLessonsAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        await using var tenantScope = await TenantTransactionScope.BeginReadAsync(databaseContext, cancellationToken);

        var lessonProgressByLessonId = await databaseContext.UserLessonProgressRecords
            .Where(progressRecord => progressRecord.UserId == userId)
            .ToDictionaryAsync(progressRecord => progressRecord.LessonId, cancellationToken);

        var topicOrderById = await databaseContext.Topics
            .ToDictionaryAsync(topic => topic.Id, topic => topic.OrderInSkill, cancellationToken);

        var allLessons = (await databaseContext.Lessons.ResolveOverrides(databaseContext)
            .ToListAsync(cancellationToken))
            .OrderBy(lesson => topicOrderById.GetValueOrDefault(lesson.TopicId))
            .ThenBy(lesson => lesson.OrderInTopic)
            .ThenBy(lesson => lesson.Id)
            .ToList();

        var lessonKinds = await GetLessonKindsAsync(allLessons.Select(lesson => lesson.Id), cancellationToken);

        var profile = await organizationProfileProvider.GetCurrentAsync(cancellationToken);
        var unresolved = new List<string>();

        var summaries = allLessons.Select(lesson =>
        {
            lessonProgressByLessonId.TryGetValue(lesson.Id, out var progressRecord);
            return new LessonSummaryDto(
                lesson.Id,
                OrganizationPlaceholderRenderer.Render(lesson.Title, profile, unresolved),
                lesson.OrderInTopic,
                topicOrderById.GetValueOrDefault(lesson.TopicId),
                progressRecord?.Status ?? LessonProgressStatuses.Locked,
                progressRecord?.BestScore ?? 0,
                lessonKinds.GetValueOrDefault(lesson.Id, LessonKinds.Practice));
        }).ToList();

        LogUnresolved(unresolved);

        return summaries;
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
        await using var tenantScope = await TenantTransactionScope.BeginReadAsync(databaseContext, cancellationToken);

        var lessonProgressByLessonId = await databaseContext.UserLessonProgressRecords
            .Where(progressRecord => progressRecord.UserId == userId)
            .ToDictionaryAsync(progressRecord => progressRecord.LessonId, cancellationToken);

        var topicOrder = await databaseContext.Topics
            .Where(topic => topic.Id == topicId)
            .Select(topic => (int?)topic.OrderInSkill)
            .FirstOrDefaultAsync(cancellationToken) ?? 0;

        var allLessons = await databaseContext.Lessons.ResolveOverrides(databaseContext)
            .Where(lesson => lesson.TopicId == topicId)
            .OrderBy(lesson => lesson.OrderInTopic)
            .ToListAsync(cancellationToken);

        var lessonKinds = await GetLessonKindsAsync(allLessons.Select(lesson => lesson.Id), cancellationToken);

        var profile = await organizationProfileProvider.GetCurrentAsync(cancellationToken);
        var unresolved = new List<string>();

        var summaries = allLessons.Select(lesson =>
        {
            lessonProgressByLessonId.TryGetValue(lesson.Id, out var progressRecord);
            return new LessonSummaryDto(
                lesson.Id,
                OrganizationPlaceholderRenderer.Render(lesson.Title, profile, unresolved),
                lesson.OrderInTopic,
                topicOrder,
                progressRecord?.Status ?? LessonProgressStatuses.Locked,
                progressRecord?.BestScore ?? 0,
                lessonKinds.GetValueOrDefault(lesson.Id, LessonKinds.Practice));
        }).ToList();

        LogUnresolved(unresolved);

        return summaries;
    }

    public async Task<IReadOnlyList<LessonSummaryDto>> GetLessonsForSkillAsync(
        Guid userId,
        string skillSlug,
        CancellationToken cancellationToken = default)
    {
        await using var tenantScope = await TenantTransactionScope.BeginReadAsync(databaseContext, cancellationToken);

        var skill = await databaseContext.Skills
            .FirstOrDefaultAsync(candidate => candidate.IconicName == skillSlug, cancellationToken);

        if (skill is null)
            return [];

        var topics = await databaseContext.Topics
            .Where(topic => topic.SkillId == skill.Id)
            .Select(topic => new { topic.Id, topic.OrderInSkill })
            .ToListAsync(cancellationToken);

        if (topics.Count == 0)
            return [];

        var topicOrderById = topics.ToDictionary(topic => topic.Id, topic => topic.OrderInSkill);
        var topicIds = topicOrderById.Keys.ToList();

        var lessonProgressByLessonId = await databaseContext.UserLessonProgressRecords
            .Where(progressRecord => progressRecord.UserId == userId)
            .ToDictionaryAsync(progressRecord => progressRecord.LessonId, cancellationToken);

        // Order across the whole skill by topic first (Topic.OrderInSkill), then by the
        // lesson's position within its topic — so topics stay grouped instead of interleaving.
        var allLessons = (await databaseContext.Lessons.ResolveOverrides(databaseContext)
            .Where(lesson => topicIds.Contains(lesson.TopicId))
            .ToListAsync(cancellationToken))
            .OrderBy(lesson => topicOrderById[lesson.TopicId])
            .ThenBy(lesson => lesson.OrderInTopic)
            .ToList();

        var lessonKinds = await GetLessonKindsAsync(allLessons.Select(lesson => lesson.Id), cancellationToken);

        var isFirstLesson = true;

        var profile = await organizationProfileProvider.GetCurrentAsync(cancellationToken);
        var unresolved = new List<string>();

        var summaries = allLessons.Select(lesson =>
        {
            lessonProgressByLessonId.TryGetValue(lesson.Id, out var progressRecord);
            var status = progressRecord?.Status
                ?? (isFirstLesson ? LessonProgressStatuses.Available : LessonProgressStatuses.Locked);
            isFirstLesson = false;
            return new LessonSummaryDto(
                lesson.Id,
                OrganizationPlaceholderRenderer.Render(lesson.Title, profile, unresolved),
                lesson.OrderInTopic,
                topicOrderById[lesson.TopicId],
                status,
                progressRecord?.BestScore ?? 0,
                lessonKinds.GetValueOrDefault(lesson.Id, LessonKinds.Practice));
        }).ToList();

        LogUnresolved(unresolved);

        return summaries;
    }

    public async Task<IReadOnlyList<ExerciseDto>> GetExercisesForLessonAsync(
        Guid lessonId,
        CancellationToken cancellationToken = default)
    {
        await using var tenantScope = await TenantTransactionScope.BeginReadAsync(databaseContext, cancellationToken);

        var rawExercises = await databaseContext.Exercises
            .Where(exercise => exercise.LessonId == lessonId)
            .OrderBy(exercise => exercise.OrderInLesson)
            .Select(exercise => new { exercise.Id, exercise.Type, exercise.OrderInLesson, exercise.SerializedContent })
            .ToListAsync(cancellationToken);

        // Phase 40.19. Loaded once for the whole lesson, before the loop: the profile cannot change
        // between two exercises of the same request, and the provider memoizes anyway.
        var profile = await organizationProfileProvider.GetCurrentAsync(cancellationToken);
        var unresolved = new List<string>();

        var rendered = rawExercises.Select(rawExercise =>
        {
            // Rendered before the answer key is stripped, not after, so a placeholder inside an
            // option's text survives; stripping only removes fields, never rewrites them.
            var renderedContent = OrganizationPlaceholderRenderer.RenderJsonStrings(
                rawExercise.SerializedContent, profile, unresolved);
            var fullContent = JsonDocument.Parse(renderedContent).RootElement;
            var learnerContent = StripAnswerKeyFields(rawExercise.Type, fullContent);
            return new ExerciseDto(
                rawExercise.Id,
                rawExercise.Type,
                rawExercise.OrderInLesson,
                learnerContent);
        }).ToList();

        LogUnresolved(unresolved);

        return rendered;
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
        // Phase 40.10. Read scope only around the lookup: the AI evaluation below is a network
        // call and must never happen with a Postgres transaction held open.
        Exercise exercise;
        await using (await TenantTransactionScope.BeginReadAsync(databaseContext, cancellationToken))
        {
            exercise = await databaseContext.Exercises
                .FirstOrDefaultAsync(exerciseRecord => exerciseRecord.Id == exerciseId, cancellationToken)
                ?? throw new KeyNotFoundException($"Exercise {exerciseId} not found.");
        }

        var evaluationStrategy = evaluationFactory.GetStrategyForExerciseType(exercise.Type);

        // Phase 40.19. The grader has to see the same words the learner saw. A question rendered as
        // «Как вы представите Кредит Плюс?» graded against the unrendered
        // «Как вы представите {{organization.product}}?» would mark a correct answer wrong — the
        // deterministic strategies compare option text, and the AI strategy is being asked to judge
        // an answer to a question it was not shown.
        var profile = await organizationProfileProvider.GetCurrentAsync(cancellationToken);
        var unresolved = new List<string>();
        var exerciseContent = JsonDocument
            .Parse(OrganizationPlaceholderRenderer.RenderJsonStrings(exercise.SerializedContent, profile, unresolved))
            .RootElement;

        LogUnresolved(unresolved);

        var evaluationResult = await evaluationStrategy.EvaluateAnswerAsync(
            exerciseContent, userAnswer, cancellationToken);

        // Phase 40.16. Resolved before the write scope below and deliberately outside it: minting a
        // lesson's first version can lose a unique-index race with another learner, and a
        // unique-index violation aborts the entire Postgres transaction it happens in — inside the
        // scope below, that would take the learner's answer down with it. The snapshot is immutable
        // once published, so reading it a moment earlier costs nothing.
        var lessonVersionId = await lessonVersionService.EnsurePublishedVersionIdAsync(
            exercise.LessonId, cancellationToken);

        var newAttempt = new UserExerciseAttempt
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            LessonVersionId = lessonVersionId,
            ExerciseId = exerciseId,
            SerializedAnswer = userAnswer.GetRawText(),
            IsCorrect = evaluationResult.IsCorrect,
            Score = evaluationResult.Score,
            SerializedAiFeedback = evaluationResult.AiFeedback is not null
                ? JsonSerializer.Serialize(new { feedback = evaluationResult.AiFeedback })
                : null,
            AttemptedAt = DateTime.UtcNow
        };

        // Phase 40.10. One explicit transaction over the whole write phase: the progress reads
        // inside UpdateLessonProgressAsync / PublishSkillCompletionIfFinishedAsync hit
        // row-level-security-protected tables and see nothing without SET LOCAL, which
        // TenantConnectionInterceptor only issues when a transaction starts. The intermediate
        // SaveChangesAsync calls still flush in order, so the sequencing the comments below
        // describe is unchanged — only the commit moved to the end.
        await using var writeScope = await TenantTransactionScope.BeginWriteAsync(databaseContext, cancellationToken);

        databaseContext.UserExerciseAttempts.Add(newAttempt);
        // Persist the attempt first so UpdateLessonProgressAsync can count it
        // when querying passed exercises (required for LE3 all-exercises-passed gate).
        await databaseContext.SaveChangesAsync(cancellationToken);

        // Lesson completion is attempt-based: a lesson is "passed" once the user has
        // attempted EVERY exercise in it, regardless of correctness. This runs on every
        // submission (correct or wrong) so a lesson can always be completed by going
        // through all of its exercises.
        var (lessonWasCompleted, lessonBestScore) = await UpdateLessonProgressAsync(
            userId, exercise.LessonId, evaluationResult.Score, lessonVersionId, cancellationToken);

        // Stage the exercise/lesson outbox rows BEFORE the commit so the progress mutations
        // and their integration events are persisted in the SAME transaction (no lost events
        // if the process crashes right after the business state commits).
        await eventPublisher.PublishExerciseCompletedAsync(
            new ExerciseCompletedEvent(userId, exercise.Type, evaluationResult.Score, evaluationResult.IsCorrect),
            cancellationToken);

        if (lessonWasCompleted)
        {
            await eventPublisher.PublishLessonCompletedAsync(
                new LessonCompletedEvent(userId, exercise.LessonId, lessonBestScore), cancellationToken);
        }

        // Atomically commit lesson/skill progress + the exercise/lesson outbox rows.
        await databaseContext.SaveChangesAsync(cancellationToken);

        if (lessonWasCompleted)
        {
            // Skill completion is decided by querying the now-committed lesson progress, so it
            // must run after the commit above; its skill.completed outbox row is flushed below.
            await PublishSkillCompletionIfFinishedAsync(userId, exercise.LessonId, cancellationToken);

            await databaseContext.SaveChangesAsync(cancellationToken);
        }

        await writeScope.CommitAsync(cancellationToken);

        return new ExerciseSubmissionResultDto(
            evaluationResult.IsCorrect,
            evaluationResult.Score,
            evaluationResult.Explanation,
            evaluationResult.AiFeedback,
            XpEarned: 0,
            NewlyUnlockedAchievementKeys: Array.Empty<string>());
    }

    /// <summary>
    /// Updates lesson progress after an answer submission.
    /// Lesson is marked complete once the user has at least one attempt (correct or wrong)
    /// for EVERY exercise in the lesson — a lesson can always be passed by going through it.
    /// BestScore = max(existing best, current score).
    /// Returns (transitionedToCompleted, bestScore).
    ///
    /// <para>
    /// Phase 40.16: <paramref name="lessonVersionId"/> is stamped on the row when it is created and
    /// refreshed only when the row actually advances — a new best score, or the transition to
    /// completed. Refreshing it on every submission would relabel a completion earned on version 1
    /// as a completion of version 3, which is the retroactive rewrite this phase exists to stop,
    /// arrived at from the progress side.
    /// </para>
    /// </summary>
    private async Task<(bool TransitionedToCompleted, int BestScore)> UpdateLessonProgressAsync(
        Guid userId,
        Guid lessonId,
        int currentScore,
        Guid? lessonVersionId,
        CancellationToken cancellationToken = default)
    {
        // Count how many distinct exercises exist in this lesson.
        var totalExercises = await databaseContext.Exercises
            .Where(e => e.LessonId == lessonId)
            .CountAsync(cancellationToken);

        // Count how many distinct exercises the user has attempted (correct or wrong).
        var attemptedExercises = await databaseContext.UserExerciseAttempts
            .Where(a => a.UserId == userId)
            .Join(databaseContext.Exercises,
                attempt => attempt.ExerciseId,
                exercise => exercise.Id,
                (attempt, exercise) => exercise)
            .Where(e => e.LessonId == lessonId)
            .Select(e => e.Id)
            .Distinct()
            .CountAsync(cancellationToken);

        var allAttempted = totalExercises > 0 && attemptedExercises >= totalExercises;

        var progressRecord = await databaseContext.UserLessonProgressRecords
            .FirstOrDefaultAsync(record => record.UserId == userId && record.LessonId == lessonId, cancellationToken);

        var transitionedToCompleted = false;
        int bestScore;

        if (progressRecord is null)
        {
            bestScore = currentScore;
            var newStatus = allAttempted ? LessonProgressStatuses.Completed : LessonProgressStatuses.Available;
            progressRecord = new UserLessonProgress
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                LessonId = lessonId,
                LessonVersionId = lessonVersionId,
                Status = newStatus,
                BestScore = bestScore,
                CompletedAt = allAttempted ? DateTime.UtcNow : null
            };
            databaseContext.UserLessonProgressRecords.Add(progressRecord);
            transitionedToCompleted = allAttempted;
        }
        else
        {
            var bestScoreImproved = currentScore > progressRecord.BestScore;
            bestScore = Math.Max(progressRecord.BestScore, currentScore);
            progressRecord.BestScore = bestScore;

            if (allAttempted && progressRecord.Status != LessonProgressStatuses.Completed)
            {
                progressRecord.Status = LessonProgressStatuses.Completed;
                progressRecord.CompletedAt = DateTime.UtcNow;
                transitionedToCompleted = true;
            }

            if (bestScoreImproved || transitionedToCompleted)
            {
                progressRecord.LessonVersionId = lessonVersionId;
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
        var nextLesson = await ResolveNextLessonInSkillAsync(completedLesson, cancellationToken);

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

    /// <summary>
    /// Resolves the lesson that follows <paramref name="completedLesson"/> in the skill's global
    /// order (topics by OrderInSkill, then lessons by OrderInTopic). This rolls over topic
    /// boundaries: finishing a topic's last lesson unlocks the first lesson of the next topic.
    /// </summary>
    private async Task<Lesson?> ResolveNextLessonInSkillAsync(
        Lesson completedLesson,
        CancellationToken cancellationToken)
    {
        // Next lesson within the same topic wins first.
        var nextInTopic = await databaseContext.Lessons.ResolveOverrides(databaseContext)
            .Where(lesson => lesson.TopicId == completedLesson.TopicId
                        && lesson.OrderInTopic > completedLesson.OrderInTopic)
            .OrderBy(lesson => lesson.OrderInTopic)
            .ThenBy(lesson => lesson.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (nextInTopic is not null) return nextInTopic;

        // Topic exhausted → find the first lesson of the next topic in the same skill.
        var currentTopic = await databaseContext.Topics
            .FirstOrDefaultAsync(topic => topic.Id == completedLesson.TopicId, cancellationToken);

        if (currentTopic is null) return null;

        var nextTopic = await databaseContext.Topics
            .Where(topic => topic.SkillId == currentTopic.SkillId
                        && topic.OrderInSkill > currentTopic.OrderInSkill)
            .OrderBy(topic => topic.OrderInSkill)
            .ThenBy(topic => topic.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (nextTopic is null) return null;

        return await databaseContext.Lessons.ResolveOverrides(databaseContext)
            .Where(lesson => lesson.TopicId == nextTopic.Id)
            .OrderBy(lesson => lesson.OrderInTopic)
            .ThenBy(lesson => lesson.Id)
            .FirstOrDefaultAsync(cancellationToken);
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

        var totalLessonCount = await databaseContext.Lessons.ResolveOverrides(databaseContext)
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
