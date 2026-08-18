using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sellevate.BuildingBlocks.Eventing;
using Sellevate.BuildingBlocks.Messaging;
using Sellevate.BuildingBlocks.Tenancy;
using Sellevate.Company.Eventing;
using Sellevate.Company.Infrastructure.Data;

namespace Sellevate.Company.Features.Companies.FollowUpReminders;

/// <summary>
/// Finds companies whose follow-up is due and not yet notified, claims them, and publishes
/// <see cref="Topics.CompanyFollowUpDue"/> for each. Invoked on a timer by
/// <see cref="FollowUpReminderBackgroundService"/>; split out as its own scoped service so the
/// due-poll/claim/publish logic can be unit-tested against an in-memory <see cref="CompanyDbContext"/>
/// without spinning up a hosted service.
///
/// <para>
/// <b>Claim before publish, and the batch is the blast radius.</b> The whole due batch is stamped
/// <c>FollowUpNotifiedAt</c> and committed first, then published one company at a time. A single
/// publish failure therefore drops only that company's reminder, but a crash between the commit and
/// the publish loop — or a broker outage that fails every publish in it — silently drops the whole
/// in-flight batch for this tick, because every claimed company already reads as notified. This is a
/// deliberate choice of <i>at most once</i> (never double-notify a due follow-up) over guaranteed
/// delivery for a single-instance, non-outbox service, with the loss bounded by
/// <see cref="FollowUpReminderOptions.BatchSize"/> rather than unbounded. A user can always force a
/// fresh reminder by rescheduling <c>NextActionAt</c>, which clears the marker. Revisit with
/// <c>BuildingBlocks/Outbox</c> if guaranteed delivery ever becomes a requirement.
/// </para>
/// </summary>
internal sealed class FollowUpReminderService(
    CompanyDbContext databaseContext,
    ITenantContext tenantContext,
    IEventPublisher eventPublisher,
    IOptions<FollowUpReminderOptions> options,
    ILogger<FollowUpReminderService> logger) : IFollowUpReminderService
{
    /// <summary>
    /// Claims and publishes every due, unnotified follow-up for the current organization. Refuses to
    /// run without a concrete one: an unset tenant is an error, never a licence to scan every
    /// customer's pipeline (docs/TENANCY/TENANCY.md §1.6), and system mode is refused just as firmly
    /// because the enumeration that legitimately uses it lives in
    /// <see cref="FollowUpReminderBackgroundService"/> and hands a concrete organization down here.
    /// </summary>
    public async Task<int> ProcessDueFollowUpsAsync(CancellationToken cancellationToken = default)
    {
        if (tenantContext.IsSystem)
        {
            throw new InvalidOperationException(
                "FollowUpReminderService must run per organization; system mode would publish reminders across every tenant.");
        }

        var organizationId = tenantContext.OrganizationId
            ?? throw new InvalidOperationException(
                "FollowUpReminderService requires an organization; an unset tenant must never mean every organization.");

        var now = DateTime.UtcNow;

        await using var transactionScope = await TenantTransactionScope.BeginWriteAsync(databaseContext, cancellationToken);

        var dueCompanies = await databaseContext.Companies
            .Where(company => company.NextActionAt != null
                               && company.NextActionAt <= now
                               && company.FollowUpNotifiedAt == null)
            .OrderBy(company => company.NextActionAt)
            .Take(options.Value.BatchSize)
            .ToListAsync(cancellationToken);

        if (dueCompanies.Count == 0)
            return 0;

        foreach (var company in dueCompanies)
        {
            company.FollowUpNotifiedAt = now;
        }

        await databaseContext.SaveChangesAsync(cancellationToken);
        await transactionScope.CommitAsync(cancellationToken);

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

                await PublishWithRetryAsync(payload, company.UserId, company.Id, organizationId, cancellationToken);

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
    /// Publishes <paramref name="payload"/>, retrying up to
    /// <see cref="FollowUpReminderOptions.PublishMaxAttempts"/> times with a linear backoff before
    /// giving up. Does not change delivery semantics: the company is already claimed
    /// (<c>FollowUpNotifiedAt</c> stamped) before this runs, and a failure on the final attempt still
    /// propagates to the caller's try/catch, which logs and moves on without retrying again this
    /// tick — this only smooths over brief transient broker blips.
    ///
    /// <para>
    /// The organization travels in the envelope rather than in the payload: a consumer's tenant is a
    /// property of the message in its hand, and notification-service reads it from there.
    /// </para>
    /// </summary>
    private async Task PublishWithRetryAsync(
        CompanyFollowUpDueEvent payload,
        Guid userId,
        Guid companyId,
        Guid organizationId,
        CancellationToken cancellationToken)
    {
        var maximumAttempts = options.Value.PublishMaxAttempts;

        for (var attempt = 1; attempt <= maximumAttempts; attempt++)
        {
            try
            {
                await eventPublisher.PublishAsync(
                    Topics.CompanyFollowUpDue,
                    userId.ToString(),
                    Topics.CompanyFollowUpDue,
                    payload,
                    organizationId: organizationId,
                    cancellationToken: cancellationToken);

                return;
            }
            catch (Exception) when (attempt < maximumAttempts)
            {
                logger.LogWarning(
                    "Publish attempt {Attempt}/{MaxAttempts} of {Topic} failed for company {CompanyId}; retrying",
                    attempt, maximumAttempts, Topics.CompanyFollowUpDue, companyId);

                await Task.Delay(
                    TimeSpan.FromMilliseconds(options.Value.PublishRetryBaseDelayMilliseconds * attempt),
                    cancellationToken);
            }
        }
    }
}
