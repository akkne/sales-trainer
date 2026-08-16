using Microsoft.EntityFrameworkCore;
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
        Guid? sourceEventId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);

        if (amount == 0)
        {
            return;
        }

        // Application-level idempotency guard: skip if this event was already processed.
        if (sourceEventId.HasValue)
        {
            // Phase 40.13: the read needs a transaction to see anything under RLS, but this method
            // deliberately does NOT wrap the whole body in one. The insert below recovers from a
            // unique violation and carries on; inside a single long transaction that violation
            // would poison the transaction and the recovery path could not run. Writes need no
            // scope of their own — EF opens an implicit transaction per SaveChangesAsync, which is
            // what triggers SET LOCAL — so scoping the read alone is both sufficient and the only
            // shape that keeps the concurrency guard working.
            bool alreadyGranted;
            await using (await TenantTransactionScope.BeginReadAsync(databaseContext, cancellationToken))
            {
                alreadyGranted = await databaseContext.UserExperiencePointsRecords
                    .AnyAsync(record => record.SourceEventId == sourceEventId, cancellationToken);
            }

            if (alreadyGranted)
            {
                logger.LogInformation(
                    "Skipping duplicate XP grant for event {SourceEventId} (user {UserId})", sourceEventId, userId);
                return;
            }
        }

        databaseContext.UserExperiencePointsRecords.Add(new UserExperiencePointsRecord
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Amount = amount,
            Source = source,
            EarnedAt = earnedAt ?? DateTime.UtcNow,
            SourceEventId = sourceEventId,
        });

        await eventPublisher.PublishExperiencePointsGrantedAsync(
            new ExperiencePointsGrantedEvent(userId, amount, source), cancellationToken);

        try
        {
            await databaseContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (sourceEventId.HasValue && IsUniqueViolation(ex))
        {
            // Race fallback: another process inserted the same SourceEventId concurrently.
            // Detach the tracked entity so the context remains usable.
            var entry = databaseContext.ChangeTracker.Entries<UserExperiencePointsRecord>()
                .FirstOrDefault(e => e.Entity.SourceEventId == sourceEventId);
            if (entry is not null)
            {
                entry.State = EntityState.Detached;
            }

            logger.LogInformation(
                "Concurrent duplicate XP grant detected for event {SourceEventId} (user {UserId}), ignoring", sourceEventId, userId);
            return;
        }

        logger.LogInformation(
            "Granted {Amount} XP to user {UserId} from {Source}", amount, userId, source);
    }

    private static bool IsUniqueViolation(DbUpdateException ex)
    {
        // Npgsql surfaces unique constraint violations as PostgresException with SqlState 23505.
        var inner = ex.InnerException;
        return inner is not null &&
               (inner.GetType().Name == "PostgresException" || inner.GetType().FullName?.Contains("Npgsql") == true) &&
               inner.Message.Contains("23505");
    }
}
