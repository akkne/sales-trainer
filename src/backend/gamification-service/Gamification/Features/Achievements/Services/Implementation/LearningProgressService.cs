using Microsoft.EntityFrameworkCore;
using Sellevate.Gamification.Features.Achievements.Models;
using Sellevate.Gamification.Features.Achievements.Services.Abstract;
using Sellevate.Gamification.Infrastructure.Data;

namespace Sellevate.Gamification.Features.Achievements.Services.Implementation;

/// <summary>
/// Maintains the per-user learning counters achievement conditions read: completed lessons and
/// whether any skill has been finished.
///
/// <para>
/// Creates the row on first use, so callers never have to. The lesson counter is incremented, not
/// recomputed — the source of truth for "how many lessons" is learning-service, and this is a running
/// projection of its events, so a replayed event would double-count. Idempotency is therefore the
/// consumer's responsibility, not this service's.
/// </para>
/// </summary>
internal sealed class LearningProgressService(GamificationDbContext databaseContext) : ILearningProgressService
{
    public async Task RecordLessonCompletedAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        await using var tenantScope = await TenantTransactionScope.BeginWriteAsync(databaseContext, cancellationToken);

        var progress = await GetOrCreateAsync(userId, cancellationToken);
        progress.CompletedLessonCount += 1;
        progress.UpdatedAt = DateTime.UtcNow;
        await databaseContext.SaveChangesAsync(cancellationToken);
        await tenantScope.CommitAsync(cancellationToken);
    }

    public async Task RecordSkillCompletedAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        await using var tenantScope = await TenantTransactionScope.BeginWriteAsync(databaseContext, cancellationToken);

        var progress = await GetOrCreateAsync(userId, cancellationToken);
        progress.HasCompletedAnySkill = true;
        progress.UpdatedAt = DateTime.UtcNow;
        await databaseContext.SaveChangesAsync(cancellationToken);
        await tenantScope.CommitAsync(cancellationToken);
    }

    private async Task<UserLearningProgress> GetOrCreateAsync(Guid userId, CancellationToken cancellationToken)
    {
        var progress = await databaseContext.UserLearningProgressRecords
            .FirstOrDefaultAsync(record => record.UserId == userId, cancellationToken);

        if (progress is null)
        {
            progress = new UserLearningProgress { UserId = userId, UpdatedAt = DateTime.UtcNow };
            databaseContext.UserLearningProgressRecords.Add(progress);
        }

        return progress;
    }
}
