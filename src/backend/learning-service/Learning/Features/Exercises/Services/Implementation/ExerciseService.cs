using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Sellevate.BuildingBlocks.ContentTemplating;
using Sellevate.Learning.Common.Constants;
using Sellevate.Learning.Eventing;
using Sellevate.Learning.Features.Content;
using Sellevate.Learning.Features.Content.Services.Abstract;
using Sellevate.Learning.Features.Exercises.Constants;
using Sellevate.Learning.Features.Exercises.Models;
using Sellevate.Learning.Features.Exercises.Services.Abstract;
using Sellevate.Learning.Features.Lessons.Models;
using Sellevate.Learning.Features.Lessons.Services.Abstract;
using Sellevate.Learning.Infrastructure.Ai;
using Sellevate.Learning.Infrastructure.Data;

namespace Sellevate.Learning.Features.Exercises.Services.Implementation;

/// <summary>
/// The learner's path through the library: which lessons they may open, what an exercise looks like
/// with the answer key removed, and what happens to their progress when they submit an answer.
///
/// <para>
/// <b>Nothing here holds a Postgres transaction across a network call.</b> The submit path reads the
/// exercise in a short read scope, closes it, calls the grader (which for AI-graded types is an HTTP
/// hop to ai-service), and only then opens the write scope. Widening the first scope to cover the
/// grading would pin a connection for the duration of a model call.
/// </para>
///
/// <para>
/// <b>Lesson completion is attempt-based, not correctness-based.</b> A lesson is passed once the
/// learner has attempted every exercise in it, right or wrong, so a lesson can always be finished by
/// working through it — deliberately, so a single hard exercise cannot strand somebody mid-programme.
/// <c>BestScore</c> is what carries how well they did, and it only ever rises.
/// </para>
///
/// <para>
/// <b>Organization placeholders are rendered in the response and never in the stored rows.</b> The
/// exercise rows and the 40.15 snapshot both keep the raw template; only what goes over the wire
/// carries the substituted text. The other way round, publishing the same base lesson in two
/// organizations would produce two different <c>ContentHash</c> values and the shared library would
/// silently fork per customer — the expensive path 40.18 exists to make rare.
/// </para>
/// </summary>
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
    /// <b>A warning, never an exception.</b> The learner sees a sentence with a word missing, which is
    /// a content bug for somebody to fix — not a reason to fail the lesson they are in the middle of.
    /// The names are de-duplicated because one unresolved placeholder in a lesson title repeats once
    /// per lesson in the list.
    /// </para>
    /// </summary>
    private void LogUnresolved(List<string> unresolved)
    {
        if (unresolved.Count > 0)
        {
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

    /// <summary>
    /// The skill's lessons in the order a learner walks them: topics by their position in the skill,
    /// then each lesson by its position in its topic, so topics stay grouped instead of interleaving.
    /// An unknown slug, or a skill with no topics, yields an empty list rather than an error.
    ///
    /// <para>
    /// <b>The first lesson is offered even with no progress row; the rest start locked.</b> That is
    /// what makes a freshly enrolled skill enterable at all. Any existing progress row wins over the
    /// default, so the unlock chain written by <c>UnlockNextLessonInTopicAsync</c> is never overridden
    /// here.
    /// </para>
    /// </summary>
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

    /// <summary>
    /// The lesson's exercises in play order, rendered for this organization and with the answer key
    /// removed.
    ///
    /// <para>
    /// <b>The order of the two transformations matters.</b> Placeholders are rendered first and the
    /// answer key stripped second, so a placeholder inside an option's text still resolves; stripping
    /// only removes fields and never rewrites them, so it cannot undo the rendering. Reversed, an
    /// option carrying a placeholder would reach the learner unrendered.
    /// </para>
    ///
    /// <para>
    /// The organization profile is loaded once for the whole lesson rather than per exercise: it cannot
    /// change between two exercises of one request, and the provider memoizes in any case.
    /// </para>
    /// </summary>
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

        var profile = await organizationProfileProvider.GetCurrentAsync(cancellationToken);
        var unresolved = new List<string>();

        var rendered = rawExercises.Select(rawExercise =>
        {
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
    /// Removes the answer-key fields of <paramref name="exerciseType"/> from a content body before it
    /// is sent to a learner.
    ///
    /// <para>
    /// <b>This is the only place the answer is withheld, so a new type defaults to leaking it.</b>
    /// Types with nothing to hide fall through the switch and return the body untouched — correct for
    /// <c>theory_card</c>, <c>match_pairs</c>, <c>rewrite</c>, <c>free_text</c> and
    /// <c>evaluate_call</c>, whose grading is either AI-side or symmetric — but that also means a type
    /// added to <see cref="ExerciseTypes"/> and forgotten here ships its own answer key to the client.
    /// The field names come from <see cref="ExerciseContentFields"/> precisely so the grader and this
    /// method cannot disagree about what the key is.
    /// </para>
    ///
    /// <para>
    /// <b>Stripping removes fields and never rewrites them.</b> Unrecognized fields are copied through
    /// verbatim, which is what lets placeholder rendering run first; only the named keys disappear. The
    /// body is rebuilt through a serialize/re-parse round trip because <see cref="JsonElement"/> is a
    /// read-only view over its parent document and cannot be edited in place.
    /// </para>
    /// </summary>
    private static JsonElement StripAnswerKeyFields(string exerciseType, JsonElement content)
    {
        string? itemArrayFieldName = null;
        string? itemFieldToStrip = null;
        string? topLevelFieldToStrip = null;

        switch (exerciseType)
        {
            case ExerciseTypes.ChooseOption:
            case ExerciseTypes.FillBlank:
                itemArrayFieldName = ExerciseContentFields.Options;
                itemFieldToStrip = ExerciseContentFields.IsCorrect;
                break;
            case ExerciseTypes.Reorder:
                itemArrayFieldName = ExerciseContentFields.Items;
                itemFieldToStrip = ExerciseContentFields.CorrectPosition;
                break;
            case ExerciseTypes.Categorize:
                itemArrayFieldName = ExerciseContentFields.Items;
                itemFieldToStrip = ExerciseContentFields.Category;
                break;
            case ExerciseTypes.SpotMistake:
                itemArrayFieldName = ExerciseContentFields.Dialogue;
                itemFieldToStrip = ExerciseContentFields.IsMistake;
                break;
            case ExerciseTypes.AiDialogue:
                topLevelFieldToStrip = ExerciseContentFields.AiPrompt;
                break;
            default:
                return content;
        }

        var learnerFields = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var contentField in content.EnumerateObject())
            learnerFields[contentField.Name] = contentField.Value;

        if (topLevelFieldToStrip is not null)
        {
            learnerFields.Remove(topLevelFieldToStrip);
        }

        if (itemArrayFieldName is not null && itemFieldToStrip is not null
            && learnerFields.TryGetValue(itemArrayFieldName, out var itemArrayValue)
            && itemArrayValue.ValueKind == JsonValueKind.Array)
        {
            var strippedItems = new List<Dictionary<string, JsonElement>>();
            foreach (var item in itemArrayValue.EnumerateArray())
            {
                var strippedItem = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
                foreach (var itemField in item.EnumerateObject())
                {
                    if (!string.Equals(itemField.Name, itemFieldToStrip, StringComparison.Ordinal))
                        strippedItem[itemField.Name] = itemField.Value;
                }
                strippedItems.Add(strippedItem);
            }

            var strippedArrayJson = JsonSerializer.Serialize(new { array = strippedItems });
            var strippedArrayDocument = JsonDocument.Parse(strippedArrayJson);
            learnerFields[itemArrayFieldName] = strippedArrayDocument.RootElement.GetProperty("array");
        }

        var learnerContentJson = JsonSerializer.Serialize(learnerFields);
        return JsonDocument.Parse(learnerContentJson).RootElement;
    }

    /// <summary>
    /// Grades one submitted answer, records the attempt, advances the learner's lesson progress, and
    /// stages the resulting integration events.
    ///
    /// <para>
    /// <b>Three phases in a fixed order, each for its own reason.</b> The exercise is read in a read
    /// scope that closes immediately, because the grading that follows can be an HTTP call to
    /// ai-service and must never run with a Postgres transaction held open. The lesson version is then
    /// resolved <em>outside</em> any scope of ours: minting a lesson's first version can lose a
    /// unique-index race with another learner, and a unique-index violation aborts the entire
    /// transaction it happens in — inside the write scope that would take the learner's answer down
    /// with it. Only then does the write scope open.
    /// </para>
    ///
    /// <para>
    /// <b>The grader sees the rendered content, not the template.</b> A question rendered as
    /// «Как вы представите Кредит Плюс?» but graded against
    /// «Как вы представите {{organization.product}}?» would mark a correct answer wrong: the
    /// deterministic strategies compare option text, and the AI strategy would be judging an answer to
    /// a question it was never shown.
    /// </para>
    ///
    /// <para>
    /// <b>The whole write phase is one transaction, and the ordering inside it is load-bearing.</b> The
    /// attempt is flushed before progress is recomputed, because the all-exercises-attempted gate counts
    /// attempt rows and would miss this one. The outbox rows are staged before the commit so progress and
    /// its events land atomically — a crash cannot leave progress advanced with no event. Skill
    /// completion is the exception: it is decided by querying committed lesson progress, so it runs after
    /// that flush and its own outbox row is flushed behind it, all still inside the one transaction.
    /// </para>
    /// </summary>
    public async Task<ExerciseSubmissionResultDto> SubmitExerciseAnswerAsync(
        Guid userId,
        Guid exerciseId,
        JsonElement userAnswer,
        CancellationToken cancellationToken = default)
    {
        Exercise exercise;
        await using (await TenantTransactionScope.BeginReadAsync(databaseContext, cancellationToken))
        {
            exercise = await databaseContext.Exercises
                .FirstOrDefaultAsync(exerciseRecord => exerciseRecord.Id == exerciseId, cancellationToken)
                ?? throw new KeyNotFoundException($"Exercise {exerciseId} not found.");
        }

        var evaluationStrategy = evaluationFactory.GetStrategyForExerciseType(exercise.Type);

        var profile = await organizationProfileProvider.GetCurrentAsync(cancellationToken);
        var unresolved = new List<string>();
        var exerciseContent = JsonDocument
            .Parse(OrganizationPlaceholderRenderer.RenderJsonStrings(exercise.SerializedContent, profile, unresolved))
            .RootElement;

        LogUnresolved(unresolved);

        var evaluationResult = await evaluationStrategy.EvaluateAnswerAsync(
            exerciseContent, userAnswer, cancellationToken);

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

        await using var writeScope = await TenantTransactionScope.BeginWriteAsync(databaseContext, cancellationToken);

        databaseContext.UserExerciseAttempts.Add(newAttempt);
        await databaseContext.SaveChangesAsync(cancellationToken);

        var (lessonWasCompleted, lessonBestScore) = await UpdateLessonProgressAsync(
            userId, exercise.LessonId, evaluationResult.Score, lessonVersionId, cancellationToken);

        await eventPublisher.PublishExerciseCompletedAsync(
            new ExerciseCompletedEvent(userId, exercise.Type, evaluationResult.Score, evaluationResult.IsCorrect),
            cancellationToken);

        if (lessonWasCompleted)
        {
            await eventPublisher.PublishLessonCompletedAsync(
                new LessonCompletedEvent(userId, exercise.LessonId, lessonBestScore), cancellationToken);
        }

        await databaseContext.SaveChangesAsync(cancellationToken);

        if (lessonWasCompleted)
        {
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
    /// Folds one graded submission into the learner's lesson progress and reports whether the lesson
    /// has just been completed, along with its best score.
    ///
    /// <para>
    /// <b>Completed means every exercise attempted, right or wrong.</b> The gate counts distinct
    /// exercises with at least one attempt, so a lesson can always be passed by working through it.
    /// <c>BestScore</c> is the maximum of the existing best and this submission and therefore never
    /// falls: a weaker retry is practice, not a demotion.
    /// </para>
    ///
    /// <para>
    /// <b>Phase 40.16: the version stamp is refreshed only when the row actually advances</b> — a new
    /// best score, or the transition to completed — and set once at creation. Refreshing it on every
    /// submission would relabel a completion earned on version 1 as a completion of version 3, which is
    /// the retroactive rewrite this phase exists to stop, arrived at from the progress side.
    /// </para>
    ///
    /// <para>
    /// A lesson with no exercises at all never completes, which keeps an empty draft lesson from
    /// counting towards a skill.
    /// </para>
    /// </summary>
    private async Task<(bool TransitionedToCompleted, int BestScore)> UpdateLessonProgressAsync(
        Guid userId,
        Guid lessonId,
        int currentScore,
        Guid? lessonVersionId,
        CancellationToken cancellationToken = default)
    {
        var totalExercises = await databaseContext.Exercises
            .Where(exercise => exercise.LessonId == lessonId)
            .CountAsync(cancellationToken);

        var attemptedExercises = await databaseContext.UserExerciseAttempts
            .Where(attempt => attempt.UserId == userId)
            .Join(databaseContext.Exercises,
                attempt => attempt.ExerciseId,
                exercise => exercise.Id,
                (attempt, exercise) => exercise)
            .Where(exercise => exercise.LessonId == lessonId)
            .Select(exercise => exercise.Id)
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

    /// <summary>
    /// Opens the lesson that follows the one just completed, creating its progress row if the learner
    /// has none.
    ///
    /// <para>
    /// Only a <c>Locked</c> row is moved to <c>Available</c>; a row already available, in progress or
    /// completed is left alone, so unlocking can never walk a learner's status backwards when they
    /// re-complete an earlier lesson.
    /// </para>
    /// </summary>
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
    /// The lesson following <paramref name="completedLesson"/> in the skill's global order — topics by
    /// their position in the skill, then lessons by their position in the topic — or
    /// <see langword="null"/> at the end of the skill.
    ///
    /// <para>
    /// <b>It rolls over topic boundaries deliberately.</b> Finishing a topic's last lesson unlocks the
    /// first lesson of the next topic, so a learner is never left with a completed topic and nothing
    /// open. Row id is the final tie-break at every level, so two lessons sharing a position cannot
    /// swap places between calls and hand the learner a different "next" each time.
    /// </para>
    /// </summary>
    private async Task<Lesson?> ResolveNextLessonInSkillAsync(
        Lesson completedLesson,
        CancellationToken cancellationToken)
    {
        var nextInTopic = await databaseContext.Lessons.ResolveOverrides(databaseContext)
            .Where(lesson => lesson.TopicId == completedLesson.TopicId
                        && lesson.OrderInTopic > completedLesson.OrderInTopic)
            .OrderBy(lesson => lesson.OrderInTopic)
            .ThenBy(lesson => lesson.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (nextInTopic is not null) return nextInTopic;

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

    /// <summary>
    /// Emits <c>skill.completed</c> when the lesson just finished was the skill's last outstanding one.
    ///
    /// <para>
    /// <b>Recomputed from committed progress rows, never counted incrementally.</b> That makes it
    /// idempotent under a replayed submission, and it is why the caller runs this after flushing the
    /// progress change rather than before. The lesson total is resolved for tenant overrides so an
    /// organization that overrode a lesson is not asked to complete both copies of it.
    /// </para>
    ///
    /// <para>
    /// A skill with no lessons emits nothing — "completed nothing" is not an achievement — and a lesson
    /// whose skill cannot be resolved is silently skipped rather than failing the submission.
    /// </para>
    /// </summary>
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
