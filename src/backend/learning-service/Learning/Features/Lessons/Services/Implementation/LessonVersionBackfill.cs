using Microsoft.EntityFrameworkCore;
using Sellevate.Learning.Common.Constants;
using Sellevate.Learning.Features.Lessons.Services.Abstract;
using Sellevate.Learning.Infrastructure.Data;

namespace Sellevate.Learning.Features.Lessons.Services.Implementation;

/// <inheritdoc />
internal sealed class LessonVersionBackfill(
    LearningDbContext databaseContext,
    ILessonVersionService lessonVersionService,
    ILogger<LessonVersionBackfill> logger) : ILessonVersionBackfill
{
    public async Task<int> BackfillMissingInitialVersionsAsync(CancellationToken cancellationToken = default)
    {
        var lessonIdsWithoutPublishedVersion = await FindLessonIdsWithoutPublishedVersionAsync(cancellationToken);
        if (lessonIdsWithoutPublishedVersion.Count == 0)
        {
            return 0;
        }

        logger.LogInformation(
            "Lesson version backfill starting for {LessonCount} lesson(s) with no published version",
            lessonIdsWithoutPublishedVersion.Count);

        var mintedVersionCount = 0;

        foreach (var lessonId in lessonIdsWithoutPublishedVersion)
        {
            try
            {
                // One transaction per lesson rather than one for all of them: a single lesson whose
                // exercise content is not valid JSON must not stop every other lesson from getting
                // its version, and a service that refuses to start over one bad content row is a
                // worse outage than a missing snapshot.
                if (await lessonVersionService.EnsurePublishedVersionIdAsync(lessonId, cancellationToken) is not null)
                {
                    mintedVersionCount++;
                }
            }
            catch (Exception mintFailure)
            {
                logger.LogError(
                    mintFailure,
                    "Lesson version backfill could not snapshot LessonId={LessonId}; its attempts stay unversioned",
                    lessonId);
            }
        }

        logger.LogInformation("Lesson version backfill created {VersionCount} initial version(s)", mintedVersionCount);

        return mintedVersionCount;
    }

    /// <summary>
    /// Runs in whatever tenant mode the caller established. At startup that is system mode, which
    /// sees the global library and, deliberately, nothing organization-owned: an organization's own
    /// lessons only exist from 40.18 onwards, and they get their first version from the same
    /// on-demand path a learner triggers, inside that organization's context, rather than from a
    /// startup job that would have to enumerate tenants to reach them.
    /// </summary>
    private async Task<IReadOnlyList<Guid>> FindLessonIdsWithoutPublishedVersionAsync(CancellationToken cancellationToken)
    {
        await using var tenantScope = await TenantTransactionScope.BeginReadAsync(databaseContext, cancellationToken);

        var lessonIdsWithPublishedVersion = databaseContext.LessonVersions
            .Where(version => version.Status == LessonVersionStatuses.Published)
            .Select(version => version.LessonId);

        return await databaseContext.Lessons
            .Where(lesson => !lessonIdsWithPublishedVersion.Contains(lesson.Id))
            .Select(lesson => lesson.Id)
            .ToListAsync(cancellationToken);
    }
}
