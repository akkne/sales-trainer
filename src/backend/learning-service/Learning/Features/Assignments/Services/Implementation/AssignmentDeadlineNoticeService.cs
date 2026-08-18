using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Sellevate.Learning.Common.Constants;
using Sellevate.Learning.Eventing;
using Sellevate.Learning.Features.Assignments.Models;
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
///
/// <para>
/// <b>Phase 40.26 added the second half of the same tick: the digest for the РОП.</b> The roadmap's
/// claim is that adoption fails on whether the РОП pushes their team, not on content — so the day
/// before the deadline the people who administer the organization are told, by name, who has not
/// opened it, and the notice carries the one-click reminder rather than a link to a report. It rides
/// the same pass, the same roster read and the same timestamp deliberately: they answer the same
/// question about the same date, and two sweeps would have meant two clocks that could disagree
/// about when "a day before" is.
/// </para>
/// </summary>
internal sealed class AssignmentDeadlineNoticeService(
    LearningDbContext databaseContext,
    IOrganizationMemberDirectory memberDirectory,
    ILearningEventPublisher eventPublisher,
    IOptions<AssignmentOptions> options,
    ILogger<AssignmentDeadlineNoticeService> logger) : IAssignmentDeadlineNoticeService
{
    /// <summary>
    /// How many names the digest spells out before it starts counting. Five: enough that a small
    /// team is listed in full and a large one is still a sentence somebody reads on a phone, and the
    /// true total travels beside it, so the number is never the thing that got truncated.
    /// </summary>
    private const int MaximumNamesInDigest = 5;

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

        OrganizationRoster roster;
        try
        {
            roster = await memberDirectory.GetRosterAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(
                exception,
                "Deadline notices were skipped this tick: the organization roster could not be read.");

            return 0;
        }

        if (roster.AdministratorIds is null)
        {
            // A rolling deploy in which identity-service is still older than 40.26. Skipping the
            // whole organization rather than sending the manager notices alone keeps one timestamp
            // honest: stamping now would mark this deadline as announced and the digest — the half
            // of the feature this block exists for — would be lost with nothing left to notice it.
            logger.LogWarning(
                "Deadline notices were skipped this tick: identity-service did not report the "
                + "organization's administrators, so the РОП digest could not be addressed.");

            return 0;
        }

        var liveMemberIds = roster.MemberIds.ToHashSet();
        var assignmentIds = dueAssignments.Select(assignment => assignment.Id).ToList();

        var openProgressRows = await databaseContext.AssignmentProgressRecords
            .AsNoTracking()
            .Where(record => assignmentIds.Contains(record.AssignmentId)
                             && record.Status != AssignmentProgressStatuses.Completed)
            .Select(record => new { record.AssignmentId, record.UserId, record.Status })
            .ToListAsync(cancellationToken);

        var unfinishedByAssignment = openProgressRows
            .GroupBy(record => record.AssignmentId)
            .ToDictionary(group => group.Key, group => group.Select(record => record.UserId).ToList());

        var notStartedByAssignment = openProgressRows
            .Where(record => record.Status == AssignmentProgressStatuses.NotStarted
                             && liveMemberIds.Contains(record.UserId))
            .GroupBy(record => record.AssignmentId)
            .ToDictionary(group => group.Key, group => group.Select(record => record.UserId).ToList());

        var displayNamesByUserId = await ReadDisplayNamesAsync(
            notStartedByAssignment.Values.SelectMany(userIds => userIds).ToList(), cancellationToken);

        var noticeCount = 0;

        foreach (var assignment in dueAssignments)
        {
            var recipients = unfinishedByAssignment.GetValueOrDefault(assignment.Id) ?? [];

            foreach (var userId in recipients.Where(liveMemberIds.Contains))
            {
                await eventPublisher.PublishAssignmentDeadlineApproachingAsync(
                    new AssignmentDeadlineApproachingEvent(
                        assignment.Id, userId, assignment.Title, assignment.Deadline!.Value),
                    cancellationToken);

                noticeCount++;
            }

            noticeCount += await PublishDigestAsync(
                assignment,
                notStartedByAssignment.GetValueOrDefault(assignment.Id) ?? [],
                roster.AdministratorIds,
                displayNamesByUserId,
                cancellationToken);

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

    /// <summary>
    /// Phase 40.26. The РОП's half of the notice: who has not started, and the button.
    ///
    /// <para>
    /// <b>Nobody has failed to start means no digest at all.</b> «Все молодцы» is the message that
    /// teaches a РОП the channel is filler, and a channel they have learned to ignore is the exact
    /// failure this block is written against — the assignment nobody does, the product that looks
    /// dead. The assignment is still stamped by the caller, so silence here costs one tick's work
    /// and no repetition.
    /// </para>
    ///
    /// <para>
    /// <b>Only <c>not_started</c>, although the sweep already knows who is under the threshold.</b>
    /// Somebody who tried four times and did not reach the bar needs coaching, and a push saying
    /// "you have not finished" is the product being obtuse at them; they are the most valuable row on
    /// 40.25's dashboard and they are deliberately not on this list. The roadmap asks for «список
    /// тех, кто не начал» and means it.
    /// </para>
    /// </summary>
    private async Task<int> PublishDigestAsync(
        Assignment assignment,
        IReadOnlyList<Guid> notStartedUserIds,
        IReadOnlyList<Guid> administratorIds,
        IReadOnlyDictionary<Guid, string> displayNamesByUserId,
        CancellationToken cancellationToken)
    {
        if (notStartedUserIds.Count == 0 || administratorIds.Count == 0)
        {
            return 0;
        }

        var names = notStartedUserIds
            .Select(userId => displayNamesByUserId.GetValueOrDefault(userId))
            .Where(displayName => !string.IsNullOrWhiteSpace(displayName))
            .Select(displayName => displayName!)
            // Ordinal rather than culture-aware, like the dashboard next door: the container's
            // culture data is not something this service controls, and which five names a digest
            // spells out must not depend on it.
            .OrderBy(displayName => displayName, StringComparer.OrdinalIgnoreCase)
            .Take(MaximumNamesInDigest)
            .ToList();

        var digestCount = 0;

        foreach (var administratorId in administratorIds)
        {
            await eventPublisher.PublishAssignmentDeadlineDigestAsync(
                new AssignmentDeadlineDigestEvent(
                    assignment.Id,
                    administratorId,
                    assignment.Title,
                    assignment.Deadline!.Value,
                    notStartedUserIds.Count,
                    names),
                cancellationToken);

            digestCount++;
        }

        return digestCount;
    }

    /// <summary>
    /// Names from <c>UserReplicas</c>, the platform-global projection of <c>user.updated</c>. An id
    /// with no replica row contributes to the count and not to the list — a digest that named
    /// somebody "unknown" would be worse than one that says "и ещё 3".
    /// </summary>
    private async Task<Dictionary<Guid, string>> ReadDisplayNamesAsync(
        IReadOnlyList<Guid> userIds,
        CancellationToken cancellationToken)
    {
        if (userIds.Count == 0)
        {
            return [];
        }

        var distinctUserIds = userIds.Distinct().ToList();

        return await databaseContext.UserReplicas
            .AsNoTracking()
            .Where(replica => distinctUserIds.Contains(replica.UserId))
            .ToDictionaryAsync(replica => replica.UserId, replica => replica.DisplayName, cancellationToken);
    }
}
