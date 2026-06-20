using Microsoft.EntityFrameworkCore;
using Sellevate.Gamification.Features.Achievements.Models;
using Sellevate.Gamification.Features.Achievements.Services.Abstract;
using Sellevate.Gamification.Infrastructure.Data;

namespace Sellevate.Gamification.Features.Achievements.Services.Implementation;

internal sealed class LearningProgressService(GamificationDbContext databaseContext) : ILearningProgressService
{
    public async Task RecordLessonCompletedAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var progress = await GetOrCreateAsync(userId, cancellationToken);
        progress.CompletedLessonCount += 1;
        progress.UpdatedAt = DateTime.UtcNow;
        await databaseContext.SaveChangesAsync(cancellationToken);
    }

    public async Task RecordSkillCompletedAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var progress = await GetOrCreateAsync(userId, cancellationToken);
        progress.HasCompletedAnySkill = true;
        progress.UpdatedAt = DateTime.UtcNow;
        await databaseContext.SaveChangesAsync(cancellationToken);
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
