using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Sellevate.BuildingBlocks.Tenancy;
using Sellevate.Learning.Common.Constants;
using Sellevate.Learning.Features.Assignments.Services.Abstract;
using Sellevate.Learning.Infrastructure.Data;

namespace Sellevate.Learning.Features.Assignments;

/// <summary>
/// Phase 40.23. Walks the organizations that have an unannounced deadline coming and warns the
/// people who have not finished (docs/TENANCY/BACKGROUND_JOBS.md §4e).
///
/// <para>
/// <b>Per-organization iteration over a system enumeration</b> — the mode 40.14 requires of anything
/// that produces user-visible output. The enumeration selects one column, organization ids, from
/// rows already known to be due; everything after it runs in a fresh scope with a concrete
/// organization set, so the queries that actually read assignments and progress rows are filtered by
/// the query filter and the row-level-security policy exactly as a request would be.
/// </para>
///
/// <para>
/// <b>Operational dependency, the same one five other jobs carry.</b> System mode issues no
/// <c>SET LOCAL app.organization_id</c>, so the enumeration returns rows only under a role that
/// bypasses row-level security. That holds today and is recorded in docs/DONT_FORGET.md as a
/// prerequisite of the <c>sellevate_app</c> rollout: under a <c>NOBYPASSRLS</c> role the list comes
/// back empty and the sweep goes quiet without erroring, which is the failure mode worth naming
/// because nothing in the logs would say so.
/// </para>
///
/// <para>
/// <b>Why the list comes from this database rather than from a tenant registry.</b> The question is
/// "which organizations have a deadline coming", which is a fact of <c>Assignments</c>. A
/// registry-driven loop would visit every customer who has never written an assignment, and — the
/// part that matters — would silently skip one whose registry row had not replicated yet. Same call
/// company-service made in 40.12.
/// </para>
/// </summary>
internal sealed class AssignmentDeadlineSweepService(
    IServiceScopeFactory scopeFactory,
    IOptions<AssignmentOptions> options,
    ILogger<AssignmentDeadlineSweepService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromMinutes(Math.Clamp(options.Value.SweepIntervalMinutes, 1, 24 * 60));
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
                logger.LogError(exception, "Assignment deadline sweep failed; will retry next tick");
            }
        }
        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken));
    }

    internal async Task<int> SweepAsync(CancellationToken cancellationToken)
    {
        var organizationIds = await EnumerateOrganizationsWithApproachingDeadlinesAsync(cancellationToken);
        if (organizationIds.Count == 0)
        {
            return 0;
        }

        var noticeCount = 0;

        foreach (var organizationId in organizationIds)
        {
            // One scope per organization, never one reused: TenantContext refuses to be re-pointed
            // at a second organization, which turns "the loop forgot to reset the tenant" from a
            // silent cross-tenant publish into an exception.
            using var scope = scopeFactory.CreateScope();
            scope.ServiceProvider.GetRequiredService<TenantContext>().SetOrganization(organizationId);

            var noticeService = scope.ServiceProvider.GetRequiredService<IAssignmentDeadlineNoticeService>();

            try
            {
                noticeCount += await noticeService.PublishDueNoticesAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                // One organization's failure must not silence every other organization's notices
                // for the rest of the tick — that is the whole reason the loop exists.
                logger.LogError(
                    exception,
                    "Assignment deadline sweep failed for organization {OrganizationId}; other organizations continue",
                    organizationId);
            }
        }

        return noticeCount;
    }

    /// <summary>
    /// The only place this service enters system mode, and the explicit, auditable opt-in
    /// docs/TENANCY/TENANCY.md §1.6 asks for. It reads one column of rows that are already due and
    /// unannounced, never their content.
    /// </summary>
    private async Task<IReadOnlyList<Guid>> EnumerateOrganizationsWithApproachingDeadlinesAsync(
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var horizon = now.AddHours(Math.Clamp(options.Value.DeadlineNoticeLeadHours, 1, 24 * 30));

        using var scope = scopeFactory.CreateScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().EnterSystemMode();

        var databaseContext = scope.ServiceProvider.GetRequiredService<LearningDbContext>();
        await using var transactionScope =
            await TenantTransactionScope.BeginReadAsync(databaseContext, cancellationToken);

        return await databaseContext.Assignments
            .IgnoreQueryFilters()
            .Where(assignment => assignment.Status == AssignmentStatuses.Active
                                 && assignment.Deadline != null
                                 && assignment.Deadline <= horizon
                                 && assignment.Deadline > now
                                 && assignment.DeadlineNoticeSentAt == null)
            .Select(assignment => assignment.OrganizationId)
            .Distinct()
            .ToListAsync(cancellationToken);
    }
}
