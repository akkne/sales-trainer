using Microsoft.EntityFrameworkCore;
using Sellevate.Learning.Common.Constants;
using Sellevate.Learning.Features.Assignments.Models;
using Sellevate.Learning.Features.Assignments.Services.Abstract;
using Sellevate.Learning.Infrastructure.Data;
using Sellevate.Learning.Infrastructure.Identity;

namespace Sellevate.Learning.Features.Assignments.Services.Implementation;

/// <summary>
/// Phase 40.25. Assembles the РОП's screen for one assignment (docs/TENANCY/ASSIGNMENTS.md §4).
///
/// <para>
/// <b>Nothing here is stored and nothing here is a new number.</b> The funnel is a count of the
/// progress rows 40.23 writes and 40.22 moves; the series is the <c>RepeatOfAssignmentId</c> link
/// 40.24 added. A denormalized funnel column would be a second writer of a fact that already has
/// one, and 40.22's rule — derive from rows, never increment — is what makes the numbers survive a
/// Kafka redelivery. It applies to the screen for the same reason it applies to the row.
/// </para>
///
/// <para>
/// <b>The roster read is fail-open, unlike the one at issue time.</b> 40.23 made
/// <c>AssignmentAudienceResolver</c> fail loudly when identity-service cannot say who works here,
/// because issuing to a guess is a silent, permanent mistake. Reading a dashboard is the opposite
/// trade: the funnel is still true without the roster, and refusing to draw it would take the whole
/// screen away to withhold one annotation. So the roster is asked for, and its absence is reported
/// as <see langword="null"/> rather than guessed at or turned into a 503.
/// </para>
/// </summary>
internal sealed class AssignmentDashboardService(
    LearningDbContext databaseContext,
    IOrganizationMemberDirectory memberDirectory,
    ILogger<AssignmentDashboardService> logger) : IAssignmentDashboardService
{
    /// <summary>
    /// The whole screen for one assignment, or <see langword="null"/> when there is no such assignment in
    /// the caller's organization.
    ///
    /// <para>
    /// A repeat points at the origin and never at another repeat (40.24), so the whole series is one
    /// predicate rather than a walk up a chain.
    /// </para>
    /// </summary>
    public async Task<AssignmentDashboardDto?> GetDashboardAsync(
        Guid assignmentId,
        CancellationToken cancellationToken = default)
    {
        await using var tenantScope = await TenantTransactionScope.BeginReadAsync(databaseContext, cancellationToken);

        var assignment = await databaseContext.Assignments
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == assignmentId, cancellationToken);

        if (assignment is null)
        {
            return null;
        }

        var originId = assignment.RepeatOfAssignmentId ?? assignment.Id;

        var series = await databaseContext.Assignments
            .AsNoTracking()
            .Where(candidate => candidate.Id == originId || candidate.RepeatOfAssignmentId == originId)
            .OrderBy(candidate => candidate.RepeatWaveIndex ?? 0)
            .ToListAsync(cancellationToken);

        var seriesIds = series.Select(wave => wave.Id).ToList();

        var progressRows = await databaseContext.AssignmentProgressRecords
            .AsNoTracking()
            .Where(record => seriesIds.Contains(record.AssignmentId))
            .Select(record => new ProgressRow(
                record.AssignmentId,
                record.UserId,
                record.Status,
                record.BestScore,
                record.AttemptCount,
                record.FirstOpenedAt,
                record.CompletedAt))
            .ToListAsync(cancellationToken);

        var roster = await TryReadRosterAsync(cancellationToken);

        var thisAssignmentRows = progressRows
            .Where(row => row.AssignmentId == assignment.Id)
            .ToList();

        var displayNames = await ReadDisplayNamesAsync(thisAssignmentRows, cancellationToken);

        var dashboardRows = thisAssignmentRows
            .Select(row => new AssignmentDashboardRowDto(
                row.UserId,
                displayNames.GetValueOrDefault(row.UserId),
                row.Status,
                row.BestScore,
                row.AttemptCount,
                row.FirstOpenedAt,
                row.CompletedAt,
                roster is null ? null : roster.Contains(row.UserId)))
            .OrderBy(row => AttentionRank(row.Status))
            .ThenByDescending(row => row.IsActiveMember ?? true)
            .ThenBy(row => row.DisplayName ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ThenBy(row => row.UserId)
            .ToList();

        var waves = series
            .Select(wave => new AssignmentWaveDto(
                wave.Id,
                wave.RepeatWaveIndex ?? 0,
                wave.Status,
                wave.ActivatedAt,
                wave.Deadline,
                BuildFunnel(progressRows.Where(row => row.AssignmentId == wave.Id), roster)))
            .ToList();

        var funnel = BuildFunnel(thisAssignmentRows, roster);

        return new AssignmentDashboardDto(
            BuildSummary(assignment, funnel),
            funnel,
            dashboardRows,
            waves,
            roster is not null);
    }

    /// <summary>
    /// The order the roadmap argues for: the people who finished the work and stayed under the bar
    /// come first, because they are the ones who need coaching and the ones a status word alone
    /// cannot distinguish from the people who never opened it (docs/TENANCY/ASSIGNMENTS.md §1.1).
    /// "Not started" is second — it is what 40.26's push is built on — and everybody who cleared the
    /// threshold is last, because nothing needs doing about them.
    /// </summary>
    private static int AttentionRank(string status) => status switch
    {
        AssignmentProgressStatuses.FailedThreshold => 0,
        AssignmentProgressStatuses.NotStarted => 1,
        AssignmentProgressStatuses.InProgress => 2,
        _ => 3,
    };

    /// <summary>
    /// The five-stage funnel over one wave's progress rows, plus the two roster-dependent counts.
    ///
    /// <para>
    /// <c>StartedCount</c> is "everybody who has done at least one piece of graded work", derived by
    /// <b>subtraction</b> rather than by listing the three "has started" statuses, so a status added later
    /// cannot silently fall out of the funnel. Do not turn it into an enumeration.
    /// </para>
    /// </summary>
    private static AssignmentFunnelDto BuildFunnel(IEnumerable<ProgressRow> rows, IReadOnlySet<Guid>? roster)
    {
        var materialized = rows as IReadOnlyList<ProgressRow> ?? rows.ToList();

        var notStarted = materialized.Count(row => row.Status == AssignmentProgressStatuses.NotStarted);
        var completed = materialized.Count(row => row.Status == AssignmentProgressStatuses.Completed);
        var failed = materialized.Count(row => row.Status == AssignmentProgressStatuses.FailedThreshold);

        var left = roster is null
            ? (int?)null
            : materialized.Count(row => !roster.Contains(row.UserId));

        return new AssignmentFunnelDto(
            AssignedCount: materialized.Count,
            NotStartedCount: notStarted,
            StartedCount: materialized.Count - notStarted,
            CompletedCount: completed,
            FailedThresholdCount: failed,
            LeftOrganizationCount: left,
            AssignedActiveCount: left is null ? null : materialized.Count - left);
    }

    /// <summary>
    /// The same summary shape the list route returns, so the screen's header and its list row are
    /// the same object rather than two that can disagree.
    ///
    /// <para>
    /// The four counts are taken from the funnel this screen already computed rather than counted again:
    /// two sets of predicates over the same rows are two places for one of them to drift, and the header
    /// disagreeing with the funnel directly underneath it is the most visible way for that to show up.
    /// </para>
    /// </summary>
    private static AssignmentSummaryDto BuildSummary(Assignment assignment, AssignmentFunnelDto funnel)
        => new(
            assignment.Id,
            assignment.Title,
            assignment.SourceType,
            assignment.Status,
            AssignmentDocumentSerializer.ReadAudienceKind(assignment.Audience),
            assignment.OpensAt,
            assignment.Deadline,
            assignment.RepeatSchedule is not null,
            assignment.RepeatOfAssignmentId,
            assignment.RepeatWaveIndex,
            AssignmentDocumentSerializer.DeserializeContent(assignment.Content).Count,
            funnel.AssignedCount,
            funnel.StartedCount,
            funnel.CompletedCount,
            funnel.FailedThresholdCount,
            assignment.CreatedBy,
            assignment.CreatedAt,
            assignment.UpdatedAt);

    /// <summary>
    /// Who still works here, or <see langword="null"/> when identity-service could not be asked.
    /// See the class remarks for why this does not throw the way the issue-time resolver does.
    /// </summary>
    private async Task<IReadOnlySet<Guid>?> TryReadRosterAsync(CancellationToken cancellationToken)
    {
        try
        {
            return (await memberDirectory.GetRosterAsync(cancellationToken)).MemberIds.ToHashSet();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(
                exception,
                "The organization roster could not be read; the assignment dashboard is being drawn without it.");

            return null;
        }
    }

    /// <summary>
    /// Names from <c>UserReplicas</c>, the platform-global projection of <c>user.updated</c>. Absent
    /// ids simply have no name — see <see cref="AssignmentDashboardRowDto"/>.
    /// </summary>
    private async Task<Dictionary<Guid, string>> ReadDisplayNamesAsync(
        IReadOnlyList<ProgressRow> rows,
        CancellationToken cancellationToken)
    {
        if (rows.Count == 0)
        {
            return [];
        }

        var userIds = rows.Select(row => row.UserId).Distinct().ToList();

        return await databaseContext.UserReplicas
            .AsNoTracking()
            .Where(replica => userIds.Contains(replica.UserId))
            .ToDictionaryAsync(replica => replica.UserId, replica => replica.DisplayName, cancellationToken);
    }

    private sealed record ProgressRow(
        Guid AssignmentId,
        Guid UserId,
        string Status,
        int? BestScore,
        int AttemptCount,
        DateTime? FirstOpenedAt,
        DateTime? CompletedAt);
}
