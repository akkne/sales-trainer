using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sellevate.BuildingBlocks.Tenancy;
using Sellevate.Company.Infrastructure.Data;

namespace Sellevate.Company.Features.Companies.FollowUpReminders;

/// <summary>
/// Polls for due company follow-ups on a fixed interval (default 5 minutes, configurable via
/// <see cref="FollowUpReminderOptions.PollIntervalMinutes"/>) and publishes
/// <c>company.followup.due</c> for each one via the scoped <see cref="IFollowUpReminderService"/>.
///
/// <para>
/// Phase 40.12 turned this from one cross-tenant scan into <b>per-tenant iteration</b>, the first
/// of the two legitimate background-job modes in docs/TENANCY/TENANCY.md §1.6 — the correct one for
/// anything producing user-visible output. Each tick enumerates the organizations that currently
/// have something due, then opens one scope per organization with that organization set on the
/// tenant context, so every query the reminder service runs is filtered by the query filter and the
/// RLS policy exactly like a request would be. The service itself refuses to run without a
/// concrete organization: an unset tenant raises, it never means "everything".
/// </para>
///
/// <para>
/// The poll interval is floored at <see cref="MinimumPollIntervalMinutes"/> so a misconfigured zero
/// cannot turn the timer into a busy loop against the database.
/// </para>
/// </summary>
internal sealed class FollowUpReminderBackgroundService(
    IServiceScopeFactory scopeFactory,
    IOptions<FollowUpReminderOptions> options,
    ILogger<FollowUpReminderBackgroundService> logger) : BackgroundService
{
    private const int MinimumPollIntervalMinutes = 1;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromMinutes(
            Math.Max(MinimumPollIntervalMinutes, options.Value.PollIntervalMinutes));
        using var timer = new PeriodicTimer(interval);

        do
        {
            try
            {
                await ProcessDueFollowUpsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Follow-up reminder poll failed; will retry next tick");
            }
        }
        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken));
    }

    /// <summary>
    /// One tick: enumerate the organizations with something due, then process each in its own scope.
    /// A scope is never reused across organizations — <c>TenantContext</c> refuses to be re-pointed
    /// at a second one, which is the guard that turns "the loop forgot to reset the tenant" from a
    /// silent cross-tenant publish into an exception. One organization's failure is logged and the
    /// loop continues, since isolating failures per organization is the whole reason the loop exists.
    /// </summary>
    internal async Task<int> ProcessDueFollowUpsAsync(CancellationToken cancellationToken)
    {
        var organizationIds = await EnumerateOrganizationsWithDueFollowUpsAsync(cancellationToken);
        if (organizationIds.Count == 0)
        {
            return 0;
        }

        var publishedCount = 0;
        foreach (var organizationId in organizationIds)
        {
            using var scope = scopeFactory.CreateScope();
            scope.ServiceProvider.GetRequiredService<TenantContext>().SetOrganization(organizationId);

            var reminderService = scope.ServiceProvider.GetRequiredService<IFollowUpReminderService>();

            try
            {
                publishedCount += await reminderService.ProcessDueFollowUpsAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "Follow-up reminder poll failed for organization {OrganizationId}; other organizations continue",
                    organizationId);
            }
        }

        if (publishedCount > 0)
        {
            logger.LogInformation(
                "Published {Count} due company follow-up reminder(s) across {OrganizationCount} organization(s)",
                publishedCount, organizationIds.Count);
        }

        return publishedCount;
    }

    /// <summary>
    /// The one place in company-service that enters system mode, and the explicit, auditable opt-in
    /// §1.6 asks for. It selects a single column — organization ids — from rows that are already due
    /// and unnotified; it never reads row content, and everything after it runs with a concrete
    /// organization set.
    ///
    /// <para>
    /// The list comes from company-db's own <c>Companies</c> table rather than from a replicated
    /// tenant registry, because the question the job actually asks is "which organizations have a
    /// follow-up due right now", which is a fact of this database. A registry-driven loop would
    /// iterate organizations that have never created a company, and — the part that matters — would
    /// silently skip an organization whose registry row had not replicated yet, turning a
    /// replication lag into a dropped reminder. See docs/DECISIONS.md (2026-08-15, "Where
    /// company-service's per-organization poll gets its organizations").
    /// </para>
    ///
    /// <para>
    /// <b>Operational dependency:</b> system mode issues no <c>SET LOCAL</c>, so this query returns
    /// rows only for a role that bypasses RLS. That holds today (the service connects as the owning
    /// superuser) and is recorded in docs/DONT_FORGET.md as a prerequisite for rolling out the
    /// <c>sellevate_app</c> role, because a <c>NOBYPASSRLS</c> role would make this return an empty
    /// list and the poll would go quiet without erroring.
    /// </para>
    /// </summary>
    private async Task<IReadOnlyList<Guid>> EnumerateOrganizationsWithDueFollowUpsAsync(
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        using var scope = scopeFactory.CreateScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().EnterSystemMode();

        var databaseContext = scope.ServiceProvider.GetRequiredService<CompanyDbContext>();
        await using var transactionScope = await TenantTransactionScope.BeginReadAsync(databaseContext, cancellationToken);

        return await databaseContext.Companies
            .IgnoreQueryFilters()
            .Where(company => company.NextActionAt != null
                              && company.NextActionAt <= now
                              && company.FollowUpNotifiedAt == null)
            .Select(company => company.OrganizationId)
            .Distinct()
            .ToListAsync(cancellationToken);
    }
}
