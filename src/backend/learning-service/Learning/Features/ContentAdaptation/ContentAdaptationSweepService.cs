using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Sellevate.BuildingBlocks.Tenancy;
using Sellevate.Learning.Common.Constants;
using Sellevate.Learning.Features.ContentAdaptation.Services.Abstract;
using Sellevate.Learning.Infrastructure.Data;
using Sellevate.Learning.Infrastructure.Ai;

namespace Sellevate.Learning.Features.ContentAdaptation;

/// <summary>
/// Phase 40.32. Advances batch content adaptation: finds the organizations with a batch still owing
/// LLM calls and answers a few of its items for each (docs/TENANCY/BACKGROUND_JOBS.md §2.1, the
/// ninth entry).
///
/// <para>
/// <b>Per-organization iteration over a system enumeration</b> — the mode 40.14 requires of anything
/// producing user-visible output, and the shape 40.27's sweep already established. The enumeration
/// selects one column, organization ids, from batches already known to be waiting; everything after
/// it runs in a fresh scope with a concrete organization set, so the queries that read the exercise
/// and write the proposal are constrained by the query filter and the row-level-security policy
/// exactly as a request would be.
/// </para>
///
/// <para>
/// <b>Why a worker rather than the request.</b> A stage is up to sixty LLM calls; done inline, the
/// administrator would hold an HTTP connection open for the better part of an hour and lose the whole
/// batch to whichever browser gave up first. An outbox event would have bought a delivery guarantee,
/// an ordering question and a dead-letter path for a state machine that is already two columns and
/// can simply be read. What is needed is a clock and a claim, which is this.
/// </para>
///
/// <para>
/// <b>Operational dependency, the same one the eight jobs above it carry.</b> System mode issues no
/// <c>SET LOCAL app.organization_id</c>, so the enumeration returns rows only under a role that
/// bypasses row-level security. Under <c>sellevate_app</c> the list comes back empty and batches sit
/// at «готовим предложения…» for ever — noticed within the hour, because a person pressed the button
/// and is watching. Recorded in docs/DONT_FORGET.md with the other eight.
/// </para>
/// </summary>
internal sealed class ContentAdaptationSweepService(
    IServiceScopeFactory scopeFactory,
    IOptions<ContentAdaptationOptions> options,
    ILogger<ContentAdaptationSweepService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(Math.Clamp(options.Value.SweepIntervalSeconds, 5, 3600));
        using var timer = new PeriodicTimer(interval);

        do
        {
            try
            {
                await SweepAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Content adaptation sweep failed; will retry next tick");
            }
        }
        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken));
    }

    /// <summary>
    /// Advances every organization's pending adaptation batches once, and returns how many items moved.
    ///
    /// <para>
    /// <b>One scope per organization, never one reused.</b> <c>TenantContext</c> refuses to be
    /// re-pointed at a second organization, which turns "the loop forgot to reset the tenant" from a
    /// silent cross-tenant write into an exception.
    /// </para>
    ///
    /// <para>
    /// <b>The batch allowance is checked before the runner claims a lease</b> (Phase 40.33). The claim
    /// is one conditional <c>UPDATE</c> that also spends an attempt, so discovering the ceiling
    /// afterwards would burn attempts on an organization that cannot be served and eventually fail its
    /// items for a reason that has nothing to do with them. Logged at Information, not Warning: an
    /// organization at its ceiling is a commercial fact, not an incident.
    /// </para>
    ///
    /// <para>
    /// <b>One organization's failure must not stall the rest of the tick</b> — the reason the
    /// per-organization loop and its catch exist at all.
    /// </para>
    /// </summary>
    internal async Task<int> SweepAsync(CancellationToken cancellationToken)
    {
        var organizationIds = await EnumerateOrganizationsWithPendingBatchesAsync(cancellationToken);
        if (organizationIds.Count == 0)
        {
            return 0;
        }

        var answeredCount = 0;

        foreach (var organizationId in organizationIds)
        {
            using var scope = scopeFactory.CreateScope();
            scope.ServiceProvider.GetRequiredService<TenantContext>().SetOrganization(organizationId);

            var quotaClient = scope.ServiceProvider.GetRequiredService<IAiQuotaClient>();
            if (!await quotaClient.HasBatchAllowanceAsync(cancellationToken))
            {
                logger.LogInformation(
                    "Skipping organization {OrganizationId} this tick — batch AI allowance is spent",
                    organizationId);
                continue;
            }

            var stepRunner = scope.ServiceProvider.GetRequiredService<IContentAdaptationStepRunner>();

            try
            {
                answeredCount += await stepRunner.RunPendingAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "Content adaptation sweep failed for organization {OrganizationId}; other organizations continue",
                    organizationId);
            }
        }

        return answeredCount;
    }

    /// <summary>
    /// The only place this service enters system mode, and the explicit, auditable opt-in
    /// docs/TENANCY/TENANCY.md §1.6 asks for. It reads one column of rows already known to be
    /// waiting, never their content — which matters here because the content of a batch's items is a
    /// customer's own exercises rewritten in their own voice.
    /// </summary>
    private async Task<IReadOnlyList<Guid>> EnumerateOrganizationsWithPendingBatchesAsync(
        CancellationToken cancellationToken)
    {
        var leaseExpiry = DateTime.UtcNow.AddMinutes(-Math.Clamp(options.Value.ClaimLeaseMinutes, 1, 120));

        using var scope = scopeFactory.CreateScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().EnterSystemMode();

        var databaseContext = scope.ServiceProvider.GetRequiredService<LearningDbContext>();
        await using var transactionScope =
            await TenantTransactionScope.BeginReadAsync(databaseContext, cancellationToken);

        return await databaseContext.ContentAdaptationJobs
            .IgnoreQueryFilters()
            .Where(job => ContentAdaptationStatuses.WorkerOwned.Contains(job.Status)
                          && (job.ClaimedAt == null || job.ClaimedAt < leaseExpiry))
            .Select(job => job.OrganizationId)
            .Distinct()
            .ToListAsync(cancellationToken);
    }
}
