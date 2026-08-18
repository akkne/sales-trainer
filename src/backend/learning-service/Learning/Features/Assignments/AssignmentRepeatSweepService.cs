using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Sellevate.BuildingBlocks.Tenancy;
using Sellevate.Learning.Common.Constants;
using Sellevate.Learning.Features.Assignments.Services.Abstract;
using Sellevate.Learning.Infrastructure.Data;

namespace Sellevate.Learning.Features.Assignments;

/// <summary>
/// Phase 40.24. Walks the organizations that have an assignment configured to repeat and issues the
/// waves whose day has come (docs/TENANCY/BACKGROUND_JOBS.md §4f).
///
/// <para>
/// <b>Per-organization iteration over a system enumeration</b> — the mode 40.14 requires of anything
/// that produces user-visible output, and the same shape <see cref="AssignmentDeadlineSweepService"/>
/// already runs in. The enumeration selects one column, organization ids, from rows that carry a
/// repeat schedule; everything after it runs in a fresh scope with a concrete organization set, so
/// the queries that read assignments and progress rows and the inserts that create a repeat are all
/// filtered by the query filter and the row-level-security policy exactly as a request would be.
/// </para>
///
/// <para>
/// <b>Operational dependency, the same one six other jobs carry.</b> System mode issues no
/// <c>SET LOCAL app.organization_id</c>, so the enumeration returns rows only under a role that
/// bypasses row-level security. That holds today and is recorded in docs/DONT_FORGET.md as a
/// prerequisite of the <c>sellevate_app</c> rollout: under a <c>NOBYPASSRLS</c> role the list comes
/// back empty and the sweep goes quiet without erroring. That is worth naming twice for this job in
/// particular, because its output is invisible by nature — nobody notices a repeat that was never
/// issued, and the product's claim that trainings turn into recurring practice would quietly become
/// false with nothing in the logs to say so.
/// </para>
///
/// <para>
/// <b>The enumeration is deliberately coarse.</b> It asks "which organizations have an assignment
/// that repeats and was issued recently enough for a wave to still be pending", not "which
/// organizations have a wave due right now" — the offsets live inside a jsonb document and deciding
/// them in SQL would put the schedule vocabulary in two places. The per-organization step then
/// computes the exact answer, usually to nothing, over a table an organization fills at the rate a
/// human writes assignments.
/// </para>
/// </summary>
internal sealed class AssignmentRepeatSweepService(
    IServiceScopeFactory scopeFactory,
    IOptions<AssignmentOptions> options,
    ILogger<AssignmentRepeatSweepService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = options.Value.EffectiveRepeatSweepInterval;
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
                logger.LogError(exception, "Assignment repeat sweep failed; will retry next tick");
            }
        }
        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken));
    }

    /// <summary>
    /// One pass over the estate. Returns how many repeat waves were issued.
    ///
    /// <para>
    /// <b>One scope per organization, never one reused.</b> <c>TenantContext</c> refuses to be
    /// re-pointed at a second organization, which turns "the loop forgot to reset the tenant" from a
    /// silent cross-tenant write into an exception.
    /// </para>
    ///
    /// <para>
    /// <b>One organization's failure is logged and stepped over</b> rather than allowed to cost every
    /// other organization its repeats for the rest of the tick — that is the whole reason the loop
    /// exists. Cancellation is the one exception that still propagates.
    /// </para>
    /// </summary>
    internal async Task<int> SweepAsync(CancellationToken cancellationToken)
    {
        var organizationIds = await EnumerateOrganizationsWithRepeatSchedulesAsync(cancellationToken);
        if (organizationIds.Count == 0)
        {
            return 0;
        }

        var repeatCount = 0;

        foreach (var organizationId in organizationIds)
        {
            using var scope = scopeFactory.CreateScope();
            scope.ServiceProvider.GetRequiredService<TenantContext>().SetOrganization(organizationId);

            var repeatService = scope.ServiceProvider.GetRequiredService<IAssignmentRepeatIssueService>();

            try
            {
                repeatCount += await repeatService.IssueDueRepeatsAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "Assignment repeat sweep failed for organization {OrganizationId}; other organizations continue",
                    organizationId);
            }
        }

        return repeatCount;
    }

    /// <summary>
    /// The only place this service enters system mode, and the explicit, auditable opt-in
    /// docs/TENANCY/TENANCY.md §1.6 asks for. It reads one column of rows that carry a repeat
    /// schedule, never their content.
    ///
    /// <para>
    /// The date bound is the catch-up window plus the longest offset the vocabulary allows, so an
    /// organization drops out of the enumeration once none of its assignments can possibly have a
    /// wave left. It is not a correctness filter — the per-organization step re-derives everything —
    /// but without it every organization that ever configured a repeat would be visited every half
    /// hour forever.
    /// </para>
    /// </summary>
    private async Task<IReadOnlyList<Guid>> EnumerateOrganizationsWithRepeatSchedulesAsync(
        CancellationToken cancellationToken)
    {
        var horizon = DateTime.UtcNow
            .AddDays(-Models.AssignmentRepeatScheduleLimits.MaximumOffsetDays)
            .AddDays(-options.Value.EffectiveRepeatCatchUpDays);

        using var scope = scopeFactory.CreateScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().EnterSystemMode();

        var databaseContext = scope.ServiceProvider.GetRequiredService<LearningDbContext>();
        await using var transactionScope =
            await TenantTransactionScope.BeginReadAsync(databaseContext, cancellationToken);

        return await databaseContext.Assignments
            .IgnoreQueryFilters()
            .Where(assignment => assignment.RepeatSchedule != null
                                 && assignment.RepeatOfAssignmentId == null
                                 && assignment.ActivatedAt != null
                                 && assignment.ActivatedAt >= horizon
                                 && assignment.Status != AssignmentStatuses.Draft)
            .Select(assignment => assignment.OrganizationId)
            .Distinct()
            .ToListAsync(cancellationToken);
    }
}
