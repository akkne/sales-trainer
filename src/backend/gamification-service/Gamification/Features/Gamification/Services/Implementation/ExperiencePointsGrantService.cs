using Microsoft.EntityFrameworkCore;
using Sellevate.Gamification.Common.Extensions;
using Sellevate.Gamification.Eventing;
using Sellevate.Gamification.Features.Gamification.Models;
using Sellevate.Gamification.Features.Gamification.Services.Abstract;
using Sellevate.Gamification.Infrastructure.Data;

namespace Sellevate.Gamification.Features.Gamification.Services.Implementation;

/// <summary>
/// Appends rows to the experience-point ledger. The ledger is append-only: a grant is never updated
/// or deleted, and every balance in the product is a sum over it.
///
/// <para>
/// <b>A grant carrying a <c>sourceEventId</c> is applied at most once, ever.</b> That is what makes
/// the service safe behind an at-least-once Kafka consumer, and it is enforced twice — by an
/// application-level check and, authoritatively, by a filtered unique index on the column. Callers
/// must pass the originating event id whenever one exists; a grant without one is trusted to be
/// deliberate and is applied unconditionally.
/// </para>
///
/// <para>
/// Phase 40.13: the idempotency read needs a transaction to see anything under row-level security,
/// but <see cref="GrantAsync"/> deliberately does <b>not</b> wrap its whole body in one. The insert
/// recovers from a unique violation and carries on; inside a single long transaction that violation
/// would poison the transaction and the recovery path could not run. Writes need no scope of their
/// own — EF opens an implicit transaction per <c>SaveChangesAsync</c>, which is what triggers
/// <c>SET LOCAL</c> — so scoping the read alone is both sufficient and the only shape that keeps the
/// concurrency guard working.
/// </para>
/// </summary>
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

        if (sourceEventId.HasValue)
        {
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
        catch (DbUpdateException exception)
            when (sourceEventId.HasValue && exception.IsUniqueConstraintViolation())
        {
            var failedEntry = databaseContext.ChangeTracker.Entries<UserExperiencePointsRecord>()
                .FirstOrDefault(entry => entry.Entity.SourceEventId == sourceEventId);
            if (failedEntry is not null)
            {
                failedEntry.State = EntityState.Detached;
            }

            logger.LogInformation(
                "Concurrent duplicate XP grant detected for event {SourceEventId} (user {UserId}), ignoring", sourceEventId, userId);
            return;
        }

        logger.LogInformation(
            "Granted {Amount} XP to user {UserId} from {Source}", amount, userId, source);
    }
}
