using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Sellevate.Learning.Common.Constants;
using Sellevate.Learning.Eventing;
using Sellevate.Learning.Features.Assignments.Services.Abstract;
using Sellevate.Learning.Infrastructure.Data;
using Sellevate.Learning.Infrastructure.Identity;

namespace Sellevate.Learning.Features.Assignments.Services.Implementation;

/// <summary>
/// Phase 40.23. One organization's share of the deadline sweep
/// (docs/TENANCY/BACKGROUND_JOBS.md §4e).
///
/// <para>
/// <b>The roster is consulted before anybody is warned.</b> A progress row outlives the person's
/// employment on purpose — it is the record that they were asked, and 40.23 never deletes one — so
/// the set of people who still have work outstanding is not the same as the set of people who should
/// hear about it. Skipping that check would mail somebody's former employer's homework deadline to
/// them for as long as the assignment stayed open. When the roster cannot be read, the sweep skips
/// this organization for this tick rather than warning everybody: a notice deferred by half an hour
/// costs nothing, and the tick that follows will pick it up because nothing has been marked as sent.
/// </para>
///
/// <para>
/// <b>Sent-ness is recorded on the assignment, not per person.</b> The notice is about a date, and
/// every unfinished recipient gets it at the same moment, so one timestamp answers "have I announced
/// this deadline". Moving the deadline clears it (that is <c>AssignmentService</c>'s job on update),
/// which is what makes an extended deadline announce itself again.
/// </para>
/// </summary>
internal sealed class AssignmentDeadlineNoticeService(
    LearningDbContext databaseContext,
    IOrganizationMemberDirectory memberDirectory,
    ILearningEventPublisher eventPublisher,
    IOptions<AssignmentOptions> options,
    ILogger<AssignmentDeadlineNoticeService> logger) : IAssignmentDeadlineNoticeService
{
    public async Task<int> PublishDueNoticesAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var horizon = now.AddHours(Math.Clamp(options.Value.DeadlineNoticeLeadHours, 1, 24 * 30));

        await using var tenantScope = await TenantTransactionScope.BeginWriteAsync(databaseContext, cancellationToken);

        var dueAssignments = await databaseContext.Assignments
            .Where(assignment => assignment.Status == AssignmentStatuses.Active
                                 && assignment.Deadline != null
                                 && assignment.Deadline <= horizon
                                 // A deadline that has already passed is not "approaching", and
                                 // warning about it would be the product telling somebody they are
                                 // about to be late when they already are. What happens at the
                                 // deadline itself is roadmap 40.26.
                                 && assignment.Deadline > now
                                 && assignment.DeadlineNoticeSentAt == null)
            .ToListAsync(cancellationToken);

        if (dueAssignments.Count == 0)
        {
            return 0;
        }

        IReadOnlyList<Guid> activeMemberIds;
        try
        {
            activeMemberIds = await memberDirectory.GetActiveMemberIdsAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(
                exception,
                "Deadline notices were skipped this tick: the organization roster could not be read.");

            return 0;
        }

        var roster = activeMemberIds.ToHashSet();
        var assignmentIds = dueAssignments.Select(assignment => assignment.Id).ToList();

        var unfinishedByAssignment = (await databaseContext.AssignmentProgressRecords
                .AsNoTracking()
                .Where(record => assignmentIds.Contains(record.AssignmentId)
                                 && record.Status != AssignmentProgressStatuses.Completed)
                .Select(record => new { record.AssignmentId, record.UserId })
                .ToListAsync(cancellationToken))
            .GroupBy(record => record.AssignmentId)
            .ToDictionary(group => group.Key, group => group.Select(record => record.UserId).ToList());

        var noticeCount = 0;

        foreach (var assignment in dueAssignments)
        {
            var recipients = unfinishedByAssignment.GetValueOrDefault(assignment.Id) ?? [];

            foreach (var userId in recipients.Where(roster.Contains))
            {
                await eventPublisher.PublishAssignmentDeadlineApproachingAsync(
                    new AssignmentDeadlineApproachingEvent(
                        assignment.Id, userId, assignment.Title, assignment.Deadline!.Value),
                    cancellationToken);

                noticeCount++;
            }

            // Marked even when nobody was warned. An assignment everybody finished has nothing to
            // announce, and leaving it unmarked would make the sweep re-examine it every half hour
            // until its deadline passed.
            assignment.DeadlineNoticeSentAt = now;
        }

        await databaseContext.SaveChangesAsync(cancellationToken);
        await tenantScope.CommitAsync(cancellationToken);

        if (noticeCount > 0)
        {
            logger.LogInformation(
                "Published {NoticeCount} assignment deadline notice(s) across {AssignmentCount} assignment(s).",
                noticeCount, dueAssignments.Count);
        }

        return noticeCount;
    }
}
