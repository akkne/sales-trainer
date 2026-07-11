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
    // Short in-process retry to absorb transient broker blips (e.g. a leader election mid-tick)
    // without changing the at-most-once delivery semantics documented below: the outer try/catch
    // still gives up and logs after these attempts are exhausted, it just takes a couple of quick
    // extra swings first (39.17 PR #21 review fast-follow).
    private const int PublishMaxAttempts = 3;
    private static readonly TimeSpan PublishRetryBaseDelay = TimeSpan.FromMilliseconds(100);

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

        // Claim before publish: stamp FollowUpNotifiedAt for the whole claimed batch (up to
        // BatchSize companies) and commit first, then publish to Kafka one-by-one.
        // Trade-off (single-instance, non-outbox service): a single publish failure only drops
        // that one company's reminder (each publish is individually try/caught below), but a
        // process crash *between* this commit and the publish loop — or a broker outage that
        // fails every publish in the loop — silently drops up to the whole in-flight batch
        // (BatchSize, default 100) for this tick, since every claimed company is already marked
        // notified. This favors "at most once" (never double-notify a due follow-up) over
        // guaranteed delivery, and keeps the blast radius bounded by BatchSize rather than
        // unbounded. The user can always force a fresh reminder for an affected company by
        // rescheduling NextActionAt, which resets FollowUpNotifiedAt. Revisit with the Outbox
        // pattern (BuildingBlocks/Outbox) if guaranteed delivery becomes a requirement.
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

                await PublishWithRetryAsync(payload, company.UserId, company.Id, cancellationToken);

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

    /// <summary>
    /// Publishes <paramref name="payload"/>, retrying up to <see cref="PublishMaxAttempts"/> times
    /// with a small linear backoff before giving up. Does not change delivery semantics: the
    /// company is already claimed (FollowUpNotifiedAt stamped) before this runs, and a failure on
    /// the final attempt still propagates to the caller's try/catch, which logs and moves on
    /// without retrying again this tick — this only smooths over brief transient broker blips.
    /// </summary>
    private async Task PublishWithRetryAsync(
        CompanyFollowUpDueEvent payload,
        Guid userId,
        Guid companyId,
        CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= PublishMaxAttempts; attempt++)
        {
            try
            {
                await eventPublisher.PublishAsync(
                    Topics.CompanyFollowUpDue,
                    userId.ToString(),
                    Topics.CompanyFollowUpDue,
                    payload,
                    cancellationToken: cancellationToken);

                return;
            }
            catch (Exception) when (attempt < PublishMaxAttempts)
            {
                logger.LogWarning(
                    "Publish attempt {Attempt}/{MaxAttempts} of {Topic} failed for company {CompanyId}; retrying",
                    attempt, PublishMaxAttempts, Topics.CompanyFollowUpDue, companyId);

                await Task.Delay(PublishRetryBaseDelay * attempt, cancellationToken);
            }
        }
    }
}
