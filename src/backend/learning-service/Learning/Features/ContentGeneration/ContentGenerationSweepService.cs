using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Sellevate.BuildingBlocks.Tenancy;
using Sellevate.Learning.Common.Constants;
using Sellevate.Learning.Features.ContentGeneration.Services.Abstract;
using Sellevate.Learning.Infrastructure.Data;

namespace Sellevate.Learning.Features.ContentGeneration;

/// <summary>
/// Phase 40.27. Advances the admin content pipeline: finds the organizations with a run waiting on an
/// LLM call and runs one step for each (docs/TENANCY/BACKGROUND_JOBS.md §2.1).
///
/// <para>
/// <b>Per-organization iteration over a system enumeration</b> — the mode 40.14 requires of anything
/// that produces user-visible output, and the eighth entry in that registry. The enumeration selects
/// one column, organization ids, from rows already known to be waiting; everything after it runs in a
/// fresh scope with a concrete organization set, so the queries that read the run and write the
/// lesson are constrained by the query filter and the row-level-security policy exactly as a request
/// would be.
/// </para>
///
/// <para>
/// <b>Why a worker at all, when the two other shapes were available.</b> Doing the call inline in the
/// approve request would mean an administrator holding an HTTP connection open for minutes and a
/// generation lost to any browser that gave up first — and the run's whole reason to exist is that
/// its two halves are minutes long and separated by a human. An outbox event would have moved the
/// same work to a consumer and bought a delivery guarantee, an ordering question and a dead-letter
/// path for a state machine that is already a column and can simply be read. What is actually needed
/// is a clock and a claim, which is this.
/// </para>
///
/// <para>
/// <b>Operational dependency, the same one the seven jobs above it carry.</b> System mode issues no
/// <c>SET LOCAL app.organization_id</c>, so the enumeration returns rows only under a role that
/// bypasses row-level security. Under <c>sellevate_app</c> the list comes back empty and the pipeline
/// goes quiet without erroring — every run would sit at «структурируем…» forever, which unlike the
/// repeat sweep somebody would notice within the hour, because a person is watching. Recorded in
/// docs/DONT_FORGET.md with the other seven.
/// </para>
/// </summary>
internal sealed class ContentGenerationSweepService(
    IServiceScopeFactory scopeFactory,
    IOptions<ContentGenerationOptions> options,
    ILogger<ContentGenerationSweepService> logger) : BackgroundService
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
                logger.LogError(exception, "Content generation sweep failed; will retry next tick");
            }
        }
        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken));
    }

    internal async Task<int> SweepAsync(CancellationToken cancellationToken)
    {
        var organizationIds = await EnumerateOrganizationsWithPendingRunsAsync(cancellationToken);
        if (organizationIds.Count == 0)
        {
            return 0;
        }

        var advancedCount = 0;

        foreach (var organizationId in organizationIds)
        {
            // One scope per organization, never one reused: TenantContext refuses to be re-pointed at
            // a second organization, which turns "the loop forgot to reset the tenant" from a silent
            // cross-tenant write into an exception.
            using var scope = scopeFactory.CreateScope();
            scope.ServiceProvider.GetRequiredService<TenantContext>().SetOrganization(organizationId);

            var stepRunner = scope.ServiceProvider.GetRequiredService<IContentGenerationStepRunner>();

            try
            {
                advancedCount += await stepRunner.RunPendingAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                // One organization's failure must not stall every other organization's runs for the
                // rest of the tick — the reason the loop exists at all.
                logger.LogError(
                    exception,
                    "Content generation sweep failed for organization {OrganizationId}; other organizations continue",
                    organizationId);
            }
        }

        return advancedCount;
    }

    /// <summary>
    /// The only place this service enters system mode, and the explicit, auditable opt-in
    /// docs/TENANCY/TENANCY.md §1.6 asks for. It reads one column of rows already known to be waiting,
    /// never their content — which matters more here than in the sweeps above, because the content of
    /// a row on this table is a customer's uploaded product deck.
    /// </summary>
    private async Task<IReadOnlyList<Guid>> EnumerateOrganizationsWithPendingRunsAsync(
        CancellationToken cancellationToken)
    {
        var leaseExpiry = DateTime.UtcNow.AddMinutes(-Math.Clamp(options.Value.ClaimLeaseMinutes, 1, 120));
        var maximumAttempts = options.Value.MaximumAttempts;

        using var scope = scopeFactory.CreateScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().EnterSystemMode();

        var databaseContext = scope.ServiceProvider.GetRequiredService<LearningDbContext>();
        await using var transactionScope =
            await TenantTransactionScope.BeginReadAsync(databaseContext, cancellationToken);

        return await databaseContext.ContentGenerationJobs
            .IgnoreQueryFilters()
            .Where(job => (job.Status == ContentGenerationJobStatuses.Structuring
                           || job.Status == ContentGenerationJobStatuses.Generating)
                          && job.Attempts < maximumAttempts
                          && (job.ClaimedAt == null || job.ClaimedAt < leaseExpiry))
            .Select(job => job.OrganizationId)
            .Distinct()
            .ToListAsync(cancellationToken);
    }
}
