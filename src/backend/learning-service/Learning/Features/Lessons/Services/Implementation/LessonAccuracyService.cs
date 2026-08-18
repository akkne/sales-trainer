using Microsoft.EntityFrameworkCore;
using Sellevate.Learning.Common.Constants;
using Sellevate.Learning.Features.Lessons.Models;
using Sellevate.Learning.Features.Lessons.Services.Abstract;
using Sellevate.Learning.Infrastructure.Data;

namespace Sellevate.Learning.Features.Lessons.Services.Implementation;

/// <inheritdoc />
internal sealed class LessonAccuracyService(LearningDbContext databaseContext) : ILessonAccuracyService
{
    /// <summary>
    /// Builds the lesson's accuracy series, one segment per run of versions that measure the same
    /// thing, plus a bucket for attempts predating versioning. <c>null</c> means the lesson does not
    /// exist or is not visible to this organization.
    ///
    /// <para>
    /// <b>Drafts are excluded, archived versions are kept.</b> A draft has never been shown to a
    /// learner, so no attempt can belong to one; an archived version was live once, and dropping it
    /// would erase the history of everyone who studied while it was.
    /// </para>
    ///
    /// <para>
    /// <b>The grouped aggregate is projected into an anonymous type on purpose.</b> A grouped
    /// projection is the one place where binding straight to a constructor is not reliably translatable
    /// by EF Core, and a query that only fails at runtime against real Postgres is not worth the
    /// tidiness; the mapping into <c>AttemptAggregate</c> happens afterwards, in memory.
    /// </para>
    /// </summary>
    public async Task<LessonAccuracySeriesDto?> GetAccuracySeriesAsync(
        Guid lessonId,
        CancellationToken cancellationToken = default)
    {
        await using var tenantScope = await TenantTransactionScope.BeginReadAsync(databaseContext, cancellationToken);

        var lessonExists = await databaseContext.Lessons
            .AnyAsync(lesson => lesson.Id == lessonId, cancellationToken);
        if (!lessonExists)
        {
            return null;
        }

        var orderedVersions = await databaseContext.LessonVersions
            .Where(version => version.LessonId == lessonId && version.Status != LessonVersionStatuses.Draft)
            .OrderBy(version => version.VersionNumber)
            .Select(version => new { version.Id, version.VersionNumber, version.IsBreaking })
            .ToListAsync(cancellationToken);

        var orderedVersionMarkers = orderedVersions
            .Select(version => new PublishedVersionMarker(version.Id, version.VersionNumber, version.IsBreaking))
            .ToList();

        var versionIds = orderedVersionMarkers.Select(version => version.VersionId).ToList();

        var aggregateRows = await databaseContext.UserExerciseAttempts
            .Where(attempt => attempt.LessonVersionId != null && versionIds.Contains(attempt.LessonVersionId.Value))
            .GroupBy(attempt => attempt.LessonVersionId!.Value)
            .Select(group => new
            {
                VersionId = group.Key,
                AttemptCount = group.Count(),
                CorrectAttemptCount = group.Count(attempt => attempt.IsCorrect),
                TotalScore = group.Sum(attempt => (long)attempt.Score),
                FirstAttemptAt = group.Min(attempt => attempt.AttemptedAt),
                LastAttemptAt = group.Max(attempt => attempt.AttemptedAt),
            })
            .ToListAsync(cancellationToken);

        var statisticsByVersionId = aggregateRows.ToDictionary(
            row => row.VersionId,
            row => new AttemptAggregate(
                row.VersionId,
                row.AttemptCount,
                row.CorrectAttemptCount,
                row.TotalScore,
                row.FirstAttemptAt,
                row.LastAttemptAt));

        return new LessonAccuracySeriesDto(
            lessonId,
            BuildSegments(orderedVersionMarkers, statisticsByVersionId),
            await AggregateUnversionedAttemptsAsync(lessonId, cancellationToken));
    }

    /// <summary>
    /// A new segment starts at the first version and at every version published as breaking.
    /// Cosmetic versions extend the segment they follow, which is what "the dashboard joins the
    /// series across cosmetic versions and splits it across semantic ones" means in practice.
    /// </summary>
    private static List<LessonAccuracySegmentDto> BuildSegments(
        IReadOnlyList<PublishedVersionMarker> orderedVersions,
        IReadOnlyDictionary<Guid, AttemptAggregate> statisticsByVersionId)
    {
        var segments = new List<LessonAccuracySegmentDto>();
        var currentVersions = new List<PublishedVersionMarker>();

        foreach (var version in orderedVersions)
        {
            if (version.IsBreaking && currentVersions.Count > 0)
            {
                segments.Add(BuildSegment(currentVersions, statisticsByVersionId));
                currentVersions = [];
            }

            currentVersions.Add(version);
        }

        if (currentVersions.Count > 0)
        {
            segments.Add(BuildSegment(currentVersions, statisticsByVersionId));
        }

        return segments;
    }

    private static LessonAccuracySegmentDto BuildSegment(
        IReadOnlyList<PublishedVersionMarker> segmentVersions,
        IReadOnlyDictionary<Guid, AttemptAggregate> statisticsByVersionId)
    {
        var attemptCount = 0;
        var correctAttemptCount = 0;
        long totalScore = 0;
        DateTime? firstAttemptAt = null;
        DateTime? lastAttemptAt = null;

        foreach (var version in segmentVersions)
        {
            if (!statisticsByVersionId.TryGetValue(version.VersionId, out var aggregate))
            {
                continue;
            }

            attemptCount += aggregate.AttemptCount;
            correctAttemptCount += aggregate.CorrectAttemptCount;
            totalScore += aggregate.TotalScore;

            if (firstAttemptAt is null || aggregate.FirstAttemptAt < firstAttemptAt)
            {
                firstAttemptAt = aggregate.FirstAttemptAt;
            }

            if (lastAttemptAt is null || aggregate.LastAttemptAt > lastAttemptAt)
            {
                lastAttemptAt = aggregate.LastAttemptAt;
            }
        }

        return new LessonAccuracySegmentDto(
            segmentVersions[0].VersionNumber,
            segmentVersions[^1].VersionNumber,
            segmentVersions.Select(version => version.VersionNumber).ToList(),
            segmentVersions.Select(version => version.VersionId).ToList(),
            segmentVersions[0].IsBreaking,
            BuildStatistics(attemptCount, correctAttemptCount, totalScore, firstAttemptAt, lastAttemptAt));
    }

    /// <summary>
    /// Pre-40.16 attempts have no version, so the only thing that can attribute them to a lesson is
    /// the exercise row they point at — the very pointer this phase stopped trusting. That is
    /// acceptable exactly here and nowhere else: this bucket does not claim to describe any
    /// particular content, it exists to say how much history is not yet bound.
    /// </summary>
    private async Task<LessonAttemptStatisticsDto> AggregateUnversionedAttemptsAsync(
        Guid lessonId,
        CancellationToken cancellationToken)
    {
        var exerciseIds = await databaseContext.Exercises
            .Where(exercise => exercise.LessonId == lessonId)
            .Select(exercise => exercise.Id)
            .ToListAsync(cancellationToken);

        if (exerciseIds.Count == 0)
        {
            return LessonAttemptStatisticsDto.Empty;
        }

        var unversionedAttempts = await databaseContext.UserExerciseAttempts
            .Where(attempt => attempt.LessonVersionId == null && exerciseIds.Contains(attempt.ExerciseId))
            .Select(attempt => new { attempt.IsCorrect, attempt.Score, attempt.AttemptedAt })
            .ToListAsync(cancellationToken);

        if (unversionedAttempts.Count == 0)
        {
            return LessonAttemptStatisticsDto.Empty;
        }

        return BuildStatistics(
            unversionedAttempts.Count,
            unversionedAttempts.Count(attempt => attempt.IsCorrect),
            unversionedAttempts.Sum(attempt => (long)attempt.Score),
            unversionedAttempts.Min(attempt => attempt.AttemptedAt),
            unversionedAttempts.Max(attempt => attempt.AttemptedAt));
    }

    private static LessonAttemptStatisticsDto BuildStatistics(
        int attemptCount,
        int correctAttemptCount,
        long totalScore,
        DateTime? firstAttemptAt,
        DateTime? lastAttemptAt)
        => attemptCount == 0
            ? LessonAttemptStatisticsDto.Empty
            : new LessonAttemptStatisticsDto(
                attemptCount,
                correctAttemptCount,
                (double)correctAttemptCount / attemptCount,
                (double)totalScore / attemptCount,
                firstAttemptAt,
                lastAttemptAt);

    private sealed record PublishedVersionMarker(Guid VersionId, int VersionNumber, bool IsBreaking);

    private sealed record AttemptAggregate(
        Guid VersionId,
        int AttemptCount,
        int CorrectAttemptCount,
        long TotalScore,
        DateTime FirstAttemptAt,
        DateTime LastAttemptAt);
}
