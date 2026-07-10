using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sellevate.BuildingBlocks.Eventing;
using Sellevate.BuildingBlocks.Messaging;
using Sellevate.Company.Eventing;
using Sellevate.Company.Infrastructure.Data;

namespace Sellevate.Company.Features.Companies.FollowUpReminders;

/// <summary>
/// Finds companies whose follow-up is due and not yet notified, claims them, and publishes
/// <see cref="Topics.CompanyFollowUpDue"/> for each. Invoked on a timer by
/// <see cref="FollowUpReminderBackgroundService"/>; split out as its own scoped service so the
/// due-poll/claim/publish logic can be unit-tested against an in-memory <see cref="CompanyDbContext"/>
/// without spinning up a hosted service.
/// </summary>
internal sealed class FollowUpReminderService(
    CompanyDbContext databaseContext,
    IEventPublisher eventPublisher,
    IOptions<FollowUpReminderOptions> options,
    ILogger<FollowUpReminderService> logger) : IFollowUpReminderService
{
    public async Task<int> ProcessDueFollowUpsAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        var dueCompanies = await databaseContext.Companies
            .Where(company => company.NextActionAt != null
                               && company.NextActionAt <= now
                               && company.FollowUpNotifiedAt == null)
            .OrderBy(company => company.NextActionAt)
            .Take(options.Value.BatchSize)
            .ToListAsync(cancellationToken);

        if (dueCompanies.Count == 0)
            return 0;

        // Claim before publish: stamp FollowUpNotifiedAt and commit first, then publish to Kafka.
        // Trade-off (single-instance, non-outbox service): if the process crashes or the Kafka
        // produce fails after this commit, that specific reminder is silently dropped rather than
        // retried on the next tick — the company is already claimed. This favors "at most once"
        // (never double-notify a due follow-up) over guaranteed delivery. The user can always force
        // a fresh reminder by rescheduling NextActionAt, which resets FollowUpNotifiedAt. Revisit
        // with the Outbox pattern (BuildingBlocks/Outbox) if guaranteed delivery becomes a requirement.
        foreach (var company in dueCompanies)
        {
            company.FollowUpNotifiedAt = now;
        }

        await databaseContext.SaveChangesAsync(cancellationToken);

        var publishedCount = 0;
        foreach (var company in dueCompanies)
        {
            try
            {
                var payload = new CompanyFollowUpDueEvent(
                    company.Id,
                    company.UserId,
                    company.Name,
                    company.NextActionAt!.Value,
                    company.NextActionNote);

                await eventPublisher.PublishAsync(
                    Topics.CompanyFollowUpDue,
                    company.UserId.ToString(),
                    Topics.CompanyFollowUpDue,
                    payload,
                    cancellationToken: cancellationToken);

                publishedCount++;
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "Failed to publish {Topic} for company {CompanyId}; already claimed, will not retry this tick",
                    Topics.CompanyFollowUpDue, company.Id);
            }
        }

        return publishedCount;
    }
}
