using Sellevate.Gamification.Eventing;
using Sellevate.Gamification.Features.Gamification.Models;
using Sellevate.Gamification.Features.Gamification.Services.Abstract;
using Sellevate.Gamification.Infrastructure.Data;

namespace Sellevate.Gamification.Features.Gamification.Services.Implementation;

internal sealed class ExperiencePointsGrantService(
    GamificationDbContext databaseContext,
    IGamificationEventPublisher eventPublisher,
    ILogger<ExperiencePointsGrantService> logger) : IExperiencePointsGrantService
{
    public async Task GrantAsync(
        Guid userId,
        int amount,
        string source,
        DateTime? earnedAt = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);

        if (amount == 0)
        {
            return;
        }

        databaseContext.UserExperiencePointsRecords.Add(new UserExperiencePointsRecord
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Amount = amount,
            Source = source,
            EarnedAt = earnedAt ?? DateTime.UtcNow,
        });

        await eventPublisher.PublishExperiencePointsGrantedAsync(
            new ExperiencePointsGrantedEvent(userId, amount, source), cancellationToken);

        await databaseContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Granted {Amount} XP to user {UserId} from {Source}", amount, userId, source);
    }
}
