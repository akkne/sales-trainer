using Microsoft.EntityFrameworkCore;
using Sellevate.Learning.Common.Constants;
using Sellevate.Learning.Eventing;
using Sellevate.Learning.Features.Assignments.Models;
using Sellevate.Learning.Infrastructure.Data;

namespace Sellevate.Learning.Features.Assignments.Services.Implementation;

/// <summary>
/// Phase 40.23, extracted in 40.24. The moment an assignment stops being a plan and becomes work
/// somebody owes: one <c>not_started</c> row and one <c>assignment.issued</c> notice per recipient,
/// staged in the caller's open transaction.
///
/// <para>
/// <b>Extracted because it now has two callers with the same obligations.</b> A human pressing
/// "issue" (<see cref="AssignmentService"/>) and the repeat sweep issuing a wave
/// (<see cref="AssignmentRepeatIssueService"/>) must write exactly the same pair of facts. Two copies
/// of "insert a row and stage an event" is two idempotency stories and two places for one of them to
/// drift into incrementing something.
/// </para>
///
/// <para>
/// <b>Additive by construction, which is what makes it safe to call twice.</b> Whoever already has a
/// row is skipped. Nothing here writes a progress <i>status</i> either, so a re-run cannot walk
/// somebody who is halfway through back to <c>not_started</c> — the only writer of a progress status
/// stays <c>AssignmentThresholdEvaluator</c> (40.22), and one writer per column is the property that
/// keeps the two numbers on the row trustworthy.
/// </para>
///
/// <para>
/// The row and the event are staged in the same transaction on purpose: an outbox row is what makes
/// "asked" and "told" atomic, so a crash between them is impossible rather than merely unlikely.
/// </para>
/// </summary>
internal static class AssignmentFanOut
{
    /// <summary>
    /// Phase 40.23. The largest fan-out one issue may perform in a single transaction.
    ///
    /// <para>
    /// A ceiling rather than paging, because the number it guards against is not a big customer — it
    /// is a mistake. Two thousand rows plus two thousand outbox rows is a transaction that commits
    /// comfortably; twenty thousand is one that holds locks long enough to be noticed, and an
    /// organization with twenty thousand people on one five-day assignment has a product problem
    /// rather than a database one. Refusing says so while the administrator is still there to read
    /// it.
    /// </para>
    /// </summary>
    public const int MaximumFanOutSize = 2000;

    /// <summary>
    /// Adds a <c>not_started</c> row for every recipient who does not have one and stages their
    /// <c>assignment.issued</c> notice. Returns how many were added. Does not save or commit —
    /// the caller owns the transaction, which is the whole point.
    ///
    /// <para>
    /// <c>OrganizationId</c> is stamped by the tenant save interceptor, like every other
    /// <c>ITenantScoped</c> insert in this service — never assigned here, so there is no second place for
    /// it to be assigned wrongly.
    /// </para>
    /// </summary>
    public static async Task<int> IssueAsync(
        LearningDbContext databaseContext,
        ILearningEventPublisher eventPublisher,
        Assignment assignment,
        IReadOnlyList<Guid> recipientIds,
        CancellationToken cancellationToken)
    {
        var alreadyIssuedTo = (await databaseContext.AssignmentProgressRecords
                .AsNoTracking()
                .Where(record => record.AssignmentId == assignment.Id)
                .Select(record => record.UserId)
                .ToListAsync(cancellationToken))
            .ToHashSet();

        var addedCount = 0;

        foreach (var userId in recipientIds)
        {
            if (userId == Guid.Empty || !alreadyIssuedTo.Add(userId))
            {
                continue;
            }

            databaseContext.AssignmentProgressRecords.Add(new AssignmentProgress
            {
                Id = Guid.NewGuid(),
                AssignmentId = assignment.Id,
                UserId = userId,
                Status = AssignmentProgressStatuses.NotStarted,
                AttemptCount = 0,
            });

            await eventPublisher.PublishAssignmentIssuedAsync(
                new AssignmentIssuedEvent(
                    assignment.Id, userId, assignment.Title, assignment.Goal, assignment.Deadline),
                cancellationToken);

            addedCount++;
        }

        return addedCount;
    }
}
