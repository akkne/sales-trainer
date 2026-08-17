using Microsoft.EntityFrameworkCore;
using Sellevate.Learning.Common.Constants;
using Sellevate.Learning.Features.Assignments.Models;
using Sellevate.Learning.Features.Assignments.Services.Abstract;
using Sellevate.Learning.Features.Lessons.Services.Implementation;
using Sellevate.Learning.Infrastructure.Data;

namespace Sellevate.Learning.Features.Assignments.Services.Implementation;

/// <summary>
/// Phase 40.22. Turns the existing scoring into a verdict on an assignment
/// (docs/TENANCY/ASSIGNMENTS.md §1.1).
///
/// <para>
/// <b>It does not score anything.</b> The roadmap is explicit that threshold evaluation reuses
/// learning-service's grading rather than growing a second one, and it does: an exercise's
/// correctness comes from <c>UserExerciseAttempt</c> rows written by the ordinary submit path, and a
/// conversation's grade comes from ai-service's own feedback score, mirrored into
/// <see cref="UserDialogScore"/> by the consumer. This class only reads those rows, counts them, and
/// compares the count against the rule.
/// </para>
///
/// <para>
/// <b>Everything is derived, which is what makes it idempotent.</b> <c>AttemptCount</c> and
/// <c>BestScore</c> are recomputed from the attempt rows on every call rather than incremented, so
/// a redelivered Kafka message, a replayed topic, or two events arriving for the same practice
/// session leave the same values behind. The alternative — a counter bumped per event — silently
/// inflates once the Redis dedupe window expires, and "tried 4 times and did not reach the bar" is
/// precisely the line a РОП acts on.
/// </para>
///
/// <para>
/// <b>Four states, and the distinction between two of them is the block's product argument.</b>
/// <c>in_progress</c> means the person has not finished what the assignment asks for;
/// <c>failed_threshold</c> means they finished it and are under the bar. Collapsing the two into
/// "not done" hides the person who needs coaching among the people who have not started, and the
/// roadmap calls that row the most valuable one on the screen.
/// </para>
/// </summary>
internal sealed class AssignmentThresholdEvaluator(
    LearningDbContext databaseContext,
    ILogger<AssignmentThresholdEvaluator> logger) : IAssignmentThresholdEvaluator
{
    public async Task<int> EvaluateForUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
        {
            return 0;
        }

        await using var tenantScope = await TenantTransactionScope.BeginWriteAsync(databaseContext, cancellationToken);

        // Completed rows are left alone on purpose: a threshold cleared once is cleared, and a later
        // weaker attempt is practice rather than a demotion (AssignmentProgress.BestScore). Excluding
        // them also means the common case — a learner with no assignments — costs one indexed lookup
        // on IX_AssignmentProgressRecords_OrganizationId_UserId_Status and nothing else.
        var openAssignments = await (
                from record in databaseContext.AssignmentProgressRecords
                join assignment in databaseContext.Assignments on record.AssignmentId equals assignment.Id
                where record.UserId == userId
                      && record.Status != AssignmentProgressStatuses.Completed
                      && assignment.Status == AssignmentStatuses.Active
                select new { Record = record, Assignment = assignment })
            .ToListAsync(cancellationToken);

        if (openAssignments.Count == 0)
        {
            return 0;
        }

        var changedCount = 0;

        foreach (var pair in openAssignments)
        {
            if (await ApplyVerdictAsync(pair.Record, pair.Assignment, cancellationToken))
            {
                changedCount++;
            }
        }

        if (changedCount == 0)
        {
            return 0;
        }

        await databaseContext.SaveChangesAsync(cancellationToken);
        await tenantScope.CommitAsync(cancellationToken);

        return changedCount;
    }

    private async Task<bool> ApplyVerdictAsync(
        AssignmentProgress record,
        Assignment assignment,
        CancellationToken cancellationToken)
    {
        var rule = AssignmentCompletionRuleReader.TryRead(assignment.CompletionRule);
        if (rule is null)
        {
            // Fails closed and says so. The row keeps whatever status it had, which means somebody
            // stays short of the threshold rather than being handed a completion nobody measured.
            logger.LogWarning(
                "Assignment {AssignmentId} has a completion rule this service cannot read; its progress is not being judged.",
                assignment.Id);

            return false;
        }

        var content = AssignmentDocumentSerializer.DeserializeContent(assignment.Content);
        var windowStart = ResolveWindowStart(assignment);

        var measurement = rule.Kind switch
        {
            AssignmentCompletionRuleKinds.DialogScore =>
                await MeasureDialoguesAsync(record.UserId, content, windowStart, rule, cancellationToken),
            AssignmentCompletionRuleKinds.ExerciseAccuracy =>
                await MeasureExerciseAccuracyAsync(record.UserId, content, windowStart, rule, cancellationToken),
            _ => null,
        };

        if (measurement is null)
        {
            logger.LogWarning(
                "Assignment {AssignmentId} carries a '{Kind}' completion rule but no content it can be measured over.",
                assignment.Id, rule.Kind);

            return false;
        }

        if (measurement.AttemptCount == 0)
        {
            // Nothing done since the assignment was issued. Deliberately not written as
            // "in_progress": a row that has never been touched is exactly the "who has not started"
            // the РОП asks about, and moving it would erase that answer.
            return false;
        }

        var nextStatus = measurement.Met
            ? AssignmentProgressStatuses.Completed
            : measurement.WorkFinished
                ? AssignmentProgressStatuses.FailedThreshold
                : AssignmentProgressStatuses.InProgress;

        var nextBestScore = measurement.Score is { } score && score > (record.BestScore ?? -1)
            ? score
            : record.BestScore;

        var nextFirstOpenedAt = record.FirstOpenedAt ?? measurement.FirstAttemptAt;
        var nextCompletedAt = nextStatus == AssignmentProgressStatuses.Completed
            ? record.CompletedAt ?? DateTime.UtcNow
            : record.CompletedAt;

        if (nextStatus == record.Status
            && nextBestScore == record.BestScore
            && measurement.AttemptCount == record.AttemptCount
            && nextFirstOpenedAt == record.FirstOpenedAt
            && nextCompletedAt == record.CompletedAt)
        {
            return false;
        }

        record.Status = nextStatus;
        record.BestScore = nextBestScore;
        record.AttemptCount = measurement.AttemptCount;
        record.FirstOpenedAt = nextFirstOpenedAt;
        record.CompletedAt = nextCompletedAt;

        logger.LogInformation(
            "Assignment progress judged AssignmentId={AssignmentId} UserId={UserId} Rule={Kind} "
            + "Status={Status} BestScore={BestScore} AttemptCount={AttemptCount}",
            assignment.Id, record.UserId, rule.Kind, record.Status, record.BestScore, record.AttemptCount);

        return true;
    }

    /// <summary>
    /// One attempt is one graded conversation on one of the assignment's scenarios, and the rule is
    /// met once <c>requiredCount</c> of them have each cleared the bar.
    ///
    /// <para>
    /// Counting conversations rather than averaging them is the point: an average lets one strong
    /// call carry two weak ones, while the skill being trained is doing it right repeatedly. It is
    /// also why <c>failed_threshold</c> is reachable here — three tries that never cleared the bar
    /// is a finished, failed attempt at the assignment, not an unfinished one.
    /// </para>
    /// </summary>
    private async Task<ThresholdMeasurement?> MeasureDialoguesAsync(
        Guid userId,
        IReadOnlyList<AssignmentContentItemDto> content,
        DateTime windowStart,
        AssignmentCompletionRule rule,
        CancellationToken cancellationToken)
    {
        var modeKeys = content
            .Where(item => item.Kind == AssignmentContentItemKinds.DialogScenario)
            .Select(item => item.Reference)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (modeKeys.Count == 0)
        {
            return null;
        }

        var scores = await databaseContext.UserDialogScores
            .AsNoTracking()
            .Where(score => score.UserId == userId
                            && modeKeys.Contains(score.DialogModeKey)
                            && score.EvaluatedAt >= windowStart)
            .Select(score => new { score.Score, score.EvaluatedAt })
            .ToListAsync(cancellationToken);

        if (scores.Count == 0)
        {
            return ThresholdMeasurement.Untouched;
        }

        var qualifyingCount = scores.Count(score => score.Score >= rule.Threshold);

        return new ThresholdMeasurement(
            AttemptCount: scores.Count,
            Score: scores.Max(score => score.Score),
            Met: qualifyingCount >= rule.RequiredCount,
            WorkFinished: scores.Count >= rule.RequiredCount,
            FirstAttemptAt: scores.Min(score => score.EvaluatedAt));
    }

    /// <summary>
    /// One attempt is one exercise submission against the assignment's pinned lesson version, and
    /// accuracy is correct submissions over all submissions — the definition
    /// <c>LessonAccuracyService</c> already reports to the admin panel, reused rather than restated.
    ///
    /// <para>
    /// <b>Two choices here carry the "not a click" property.</b> First, accuracy counts submissions
    /// rather than exercises-eventually-answered-correctly, so brute-forcing a set until everything
    /// is green lowers the number instead of raising it. Second, the score is withheld until every
    /// exercise in the set has been attempted at least once: one lucky answer out of twenty is 100%
    /// accuracy and it would otherwise complete the assignment outright.
    /// </para>
    ///
    /// <para>
    /// <b>Attempts are matched by exercise id, not by lesson version id</b>, even though 40.16 binds
    /// every attempt to a version. The pinned snapshot decides <i>which exercises</i> the threshold
    /// covers — that is what makes the bar describe content somebody can still read — but the
    /// learner's submit path binds their attempt to whatever version is published the day they
    /// answer. Filtering on the pinned id would therefore make an assignment silently unreachable
    /// the moment its lesson is republished mid-flight, which is a worse failure than counting an
    /// attempt on a slightly newer wording of the same exercise.
    /// </para>
    /// </summary>
    private async Task<ThresholdMeasurement?> MeasureExerciseAccuracyAsync(
        Guid userId,
        IReadOnlyList<AssignmentContentItemDto> content,
        DateTime windowStart,
        AssignmentCompletionRule rule,
        CancellationToken cancellationToken)
    {
        var lessonVersionIds = content
            .Where(item => item.Kind == AssignmentContentItemKinds.LessonVersion)
            .Select(item => Guid.TryParse(item.Reference, out var lessonVersionId) ? lessonVersionId : Guid.Empty)
            .Where(lessonVersionId => lessonVersionId != Guid.Empty)
            .Distinct()
            .ToList();

        if (lessonVersionIds.Count == 0)
        {
            return null;
        }

        var snapshots = await databaseContext.LessonVersions
            .AsNoTracking()
            .Where(version => lessonVersionIds.Contains(version.Id))
            .Select(version => version.Content)
            .ToListAsync(cancellationToken);

        var exerciseIds = snapshots
            .SelectMany(LessonSnapshotSerializer.ReadExerciseIds)
            .Distinct()
            .ToList();

        if (exerciseIds.Count == 0)
        {
            // The pinned version is gone or unreadable. Refusing to judge is the only safe answer:
            // an empty set would make accuracy undefined and "0 of 0 correct" is not a failure.
            return null;
        }

        var attempts = await databaseContext.UserExerciseAttempts
            .AsNoTracking()
            .Where(attempt => attempt.UserId == userId
                              && exerciseIds.Contains(attempt.ExerciseId)
                              && attempt.AttemptedAt >= windowStart)
            .Select(attempt => new { attempt.ExerciseId, attempt.IsCorrect, attempt.AttemptedAt })
            .ToListAsync(cancellationToken);

        if (attempts.Count == 0)
        {
            return ThresholdMeasurement.Untouched;
        }

        var everyExerciseAttempted = attempts
            .Select(attempt => attempt.ExerciseId)
            .Distinct()
            .Count() >= exerciseIds.Count;

        var accuracyPercent = (int)Math.Round(
            100.0 * attempts.Count(attempt => attempt.IsCorrect) / attempts.Count,
            MidpointRounding.AwayFromZero);

        return new ThresholdMeasurement(
            AttemptCount: attempts.Count,
            Score: everyExerciseAttempted ? accuracyPercent : null,
            Met: everyExerciseAttempted && accuracyPercent >= rule.Threshold,
            WorkFinished: everyExerciseAttempted,
            FirstAttemptAt: attempts.Min(attempt => attempt.AttemptedAt));
    }

    /// <summary>
    /// Work done before the assignment existed does not count towards it. The window opens at the
    /// later of "issued" and "opens at": an assignment issued today with an opening date next Monday
    /// is asking for practice next week, and crediting last week's would be the same lie as counting
    /// a click.
    /// </summary>
    private static DateTime ResolveWindowStart(Assignment assignment)
    {
        var issuedAt = assignment.ActivatedAt ?? assignment.CreatedAt;

        return assignment.OpensAt is { } opensAt && opensAt > issuedAt ? opensAt : issuedAt;
    }

    /// <summary>
    /// What the existing scoring says about one person's standing on one assignment, in the terms
    /// <c>AssignmentProgressRecords</c> stores.
    /// </summary>
    /// <param name="Score">
    /// The 0–100 result of this measurement, or <see langword="null"/> when there is not yet enough
    /// evidence to state one. Null is not zero: a person halfway through a set has no accuracy yet,
    /// and reporting the accuracy of their first two answers would put a 100 on the РОП's screen
    /// next to somebody who has answered two questions.
    /// </param>
    private sealed record ThresholdMeasurement(
        int AttemptCount,
        int? Score,
        bool Met,
        bool WorkFinished,
        DateTime? FirstAttemptAt)
    {
        /// <summary>Nothing has been done since the assignment was issued.</summary>
        public static ThresholdMeasurement Untouched { get; } = new(0, null, false, false, null);
    }
}
