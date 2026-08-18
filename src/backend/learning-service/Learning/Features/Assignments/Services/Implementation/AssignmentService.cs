using Microsoft.EntityFrameworkCore;
using Sellevate.Learning.Common.Constants;
using Sellevate.Learning.Eventing;
using Sellevate.Learning.Features.Assignments.Models;
using Sellevate.Learning.Features.Assignments.Services.Abstract;
using Sellevate.Learning.Infrastructure.Data;

namespace Sellevate.Learning.Features.Assignments.Services.Implementation;

/// <summary>
/// Phase 40.21. Implements docs/TENANCY/ASSIGNMENTS.md §1: create a draft, say what it asks for and
/// of whom, issue it, close it.
///
/// <para>
/// <b>The one thing to keep true while changing this file.</b> Not a single write here touches
/// <c>Lessons</c>, <c>LessonVersions</c>, <c>Exercises</c> or <c>ReferenceMaterials</c>. An assignment
/// is an ordered list of references to content that already exists, exactly as a programme version is
/// — which is what lets an organization issue targeted practice without forking the library
/// (docs/TENANCY/CONTENT_MODEL.md §1) and what makes "no new renderers" (roadmap 40.23) true rather
/// than aspirational.
/// </para>
///
/// <para>
/// <b>What is not here, and is not an oversight.</b> The completion rule is evaluated by
/// <c>AssignmentThresholdEvaluator</c> (40.22) and the repeat schedule is acted on by
/// <c>AssignmentRepeatIssueService</c> (40.24) — this file owns only what a human does by hand.
/// A repeat therefore never passes through <see cref="CreateAsync"/> or <see cref="ActivateAsync"/>:
/// it is created already active by the sweep, because "configured once, then automatic" is the
/// entire feature and a draft waiting for somebody to press activate is not automatic.
/// </para>
/// </summary>
internal sealed class AssignmentService(
    LearningDbContext databaseContext,
    IAssignmentAudienceResolver audienceResolver,
    ILearningEventPublisher eventPublisher,
    ILogger<AssignmentService> logger) : IAssignmentService
{
    public async Task<IReadOnlyList<AssignmentSummaryDto>> GetAssignmentsAsync(
        string? status = null,
        CancellationToken cancellationToken = default)
    {
        await using var tenantScope = await TenantTransactionScope.BeginReadAsync(databaseContext, cancellationToken);

        var query = databaseContext.Assignments.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(status))
        {
            var requestedStatus = status.Trim();
            if (!AssignmentStatuses.IsKnown(requestedStatus))
            {
                throw new AssignmentValidationException($"'{status}' is not a known assignment status.");
            }

            query = query.Where(assignment => assignment.Status == requestedStatus);
        }

        var rows = await query
            .OrderByDescending(assignment => assignment.CreatedAt)
            .ThenByDescending(assignment => assignment.Id)
            .Select(assignment => new
            {
                assignment.Id,
                assignment.Title,
                assignment.SourceType,
                assignment.Status,
                assignment.Audience,
                assignment.Content,
                assignment.OpensAt,
                assignment.Deadline,
                assignment.RepeatSchedule,
                assignment.RepeatOfAssignmentId,
                assignment.RepeatWaveIndex,
                assignment.CreatedBy,
                assignment.CreatedAt,
                assignment.UpdatedAt,
                AssignedCount = databaseContext.AssignmentProgressRecords
                    .Count(record => record.AssignmentId == assignment.Id),
                StartedCount = databaseContext.AssignmentProgressRecords
                    .Count(record => record.AssignmentId == assignment.Id
                                     && record.Status != AssignmentProgressStatuses.NotStarted),
                CompletedCount = databaseContext.AssignmentProgressRecords
                    .Count(record => record.AssignmentId == assignment.Id
                                     && record.Status == AssignmentProgressStatuses.Completed),
                FailedThresholdCount = databaseContext.AssignmentProgressRecords
                    .Count(record => record.AssignmentId == assignment.Id
                                     && record.Status == AssignmentProgressStatuses.FailedThreshold),
            })
            .ToListAsync(cancellationToken);

        return rows
            .Select(row => new AssignmentSummaryDto(
                row.Id,
                row.Title,
                row.SourceType,
                row.Status,
                AssignmentDocumentSerializer.ReadAudienceKind(row.Audience),
                row.OpensAt,
                row.Deadline,
                row.RepeatSchedule is not null,
                row.RepeatOfAssignmentId,
                row.RepeatWaveIndex,
                AssignmentDocumentSerializer.DeserializeContent(row.Content).Count,
                row.AssignedCount,
                row.StartedCount,
                row.CompletedCount,
                row.FailedThresholdCount,
                row.CreatedBy,
                row.CreatedAt,
                row.UpdatedAt))
            .ToList();
    }

    public async Task<AssignmentDto?> GetAssignmentAsync(
        Guid assignmentId,
        CancellationToken cancellationToken = default)
    {
        await using var tenantScope = await TenantTransactionScope.BeginReadAsync(databaseContext, cancellationToken);

        var assignment = await databaseContext.Assignments
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == assignmentId, cancellationToken);

        return assignment is null ? null : ToDto(assignment);
    }

    public async Task<AssignmentDto> CreateAsync(
        Guid? actorId,
        CreateAssignmentRequestDto requestDto,
        CancellationToken cancellationToken = default)
    {
        var title = RequireTitle(requestDto.Title);
        var goal = NormalizeGoal(requestDto.Goal);
        var sourceType = RequireSourceType(requestDto.SourceType);
        var sourceRef = NormalizeSourceRef(sourceType, requestDto.SourceRef);
        RequireConsistentSchedule(requestDto.OpensAt, requestDto.Deadline);

        var now = DateTime.UtcNow;

        var assignment = new Assignment
        {
            Id = Guid.NewGuid(),
            CreatedBy = actorId,
            Title = title,
            Goal = goal,
            SourceType = sourceType,
            SourceRef = sourceRef,
            Content = AssignmentDocumentSerializer.SerializeContent(requestDto.Content),
            Audience = AssignmentDocumentSerializer.SerializeAudience(requestDto.Audience),
            OpensAt = requestDto.OpensAt,
            Deadline = requestDto.Deadline,
            CompletionRule = AssignmentDocumentSerializer.SerializeCompletionRule(requestDto.CompletionRule),
            RepeatSchedule = AssignmentDocumentSerializer.SerializeRepeatSchedule(requestDto.RepeatSchedule),
            Status = AssignmentStatuses.Draft,
            CreatedAt = now,
            UpdatedAt = now,
        };

        await using var tenantScope = await TenantTransactionScope.BeginWriteAsync(databaseContext, cancellationToken);

        databaseContext.Assignments.Add(assignment);
        await databaseContext.SaveChangesAsync(cancellationToken);
        await tenantScope.CommitAsync(cancellationToken);

        logger.LogInformation(
            "Assignment draft created AssignmentId={AssignmentId} SourceType={SourceType} ActorId={ActorId}",
            assignment.Id, assignment.SourceType, actorId);

        return ToDto(assignment);
    }

    public async Task<AssignmentWriteResult> UpdateAsync(
        Guid assignmentId,
        UpdateAssignmentRequestDto requestDto,
        CancellationToken cancellationToken = default)
    {
        var title = RequireTitle(requestDto.Title);
        var goal = NormalizeGoal(requestDto.Goal);
        var sourceType = RequireSourceType(requestDto.SourceType);
        var sourceRef = NormalizeSourceRef(sourceType, requestDto.SourceRef);
        RequireConsistentSchedule(requestDto.OpensAt, requestDto.Deadline);

        var content = AssignmentDocumentSerializer.SerializeContent(requestDto.Content);
        var audience = AssignmentDocumentSerializer.SerializeAudience(requestDto.Audience);
        var completionRule = AssignmentDocumentSerializer.SerializeCompletionRule(requestDto.CompletionRule);
        var repeatSchedule = AssignmentDocumentSerializer.SerializeRepeatSchedule(requestDto.RepeatSchedule);

        // Phase 40.23. Editing an issued assignment's audience is an ordinary act the 40.21 freeze
        // deliberately allows, so an update has to be able to fan out. Resolving the roster needs a
        // call to identity-service, and that call must not happen with a write transaction open —
        // hence the pre-flight read. It costs one extra query on a rare route and keeps a network
        // round trip out of a transaction that holds locks on the progress table.
        var statusBeforeUpdate = await ReadStatusAsync(assignmentId, cancellationToken);
        var recipientIds = statusBeforeUpdate == AssignmentStatuses.Active
            ? await ResolveRecipientsAsync(
                AssignmentDocumentSerializer.DeserializeAudience(audience), cancellationToken)
            : null;

        await using var tenantScope = await TenantTransactionScope.BeginWriteAsync(databaseContext, cancellationToken);

        var assignment = await databaseContext.Assignments
            .FirstOrDefaultAsync(candidate => candidate.Id == assignmentId, cancellationToken);
        if (assignment is null)
        {
            return AssignmentWriteResult.NotFound();
        }

        if (assignment.Status == AssignmentStatuses.Closed)
        {
            return AssignmentWriteResult.RejectedByStatus("A closed assignment cannot be edited.");
        }

        if (assignment.Status != AssignmentStatuses.Draft)
        {
            // Refused rather than ignored: an administrator who believes they moved a threshold and
            // did not is worse off than one who is told they cannot. The freeze trigger says the same
            // thing one layer down, where it cannot be bypassed.
            var frozenFields = CollectFrozenFieldChanges(
                assignment, sourceType, sourceRef, content, completionRule);

            if (frozenFields.Count > 0)
            {
                return AssignmentWriteResult.RejectedByStatus(
                    $"An issued assignment is frozen on: {string.Join(", ", frozenFields)}. "
                    + "Close it and create a new one instead.");
            }
        }

        assignment.Title = title;
        assignment.Goal = goal;
        assignment.SourceType = sourceType;
        assignment.SourceRef = sourceRef;
        assignment.Content = content;
        assignment.Audience = audience;
        // Phase 40.23. Moving the deadline re-arms its notice. A РОП who extends a due date is
        // asking for the team to be told about the new one, and the notification's dedupe key
        // carries the deadline for the same reason — otherwise the extension would be announced to
        // nobody and the original warning would stand for a date that no longer exists.
        if (assignment.Deadline != requestDto.Deadline)
        {
            assignment.DeadlineNoticeSentAt = null;
        }

        assignment.OpensAt = requestDto.OpensAt;
        assignment.Deadline = requestDto.Deadline;
        assignment.CompletionRule = completionRule;
        assignment.RepeatSchedule = repeatSchedule;
        assignment.UpdatedAt = DateTime.UtcNow;

        // Phase 40.23. Top-up, never removal. Somebody added to a running assignment gets a row and
        // is told; somebody taken out of the audience keeps theirs, because the row is the record
        // that they were asked and deleting it would rewrite what already happened — the same
        // argument that made the foreign key RESTRICT in 40.21. It also means re-saving a
        // whole_team assignment is how a РОП brings a new hire into work that is already running,
        // which is the only answer this block has for people who arrive after the issue.
        if (recipientIds is not null && assignment.Status == AssignmentStatuses.Active)
        {
            var addedCount = await AssignmentFanOut.IssueAsync(
                databaseContext, eventPublisher, assignment, recipientIds, cancellationToken);
            if (addedCount > 0)
            {
                logger.LogInformation(
                    "Assignment audience widened AssignmentId={AssignmentId} AddedRecipients={AddedCount}",
                    assignment.Id, addedCount);
            }
        }

        await databaseContext.SaveChangesAsync(cancellationToken);
        await tenantScope.CommitAsync(cancellationToken);

        return AssignmentWriteResult.Applied(ToDto(assignment));
    }

    /// <summary>
    /// Phase 40.23. Issuing is where an audience rule becomes named people
    /// (docs/TENANCY/ASSIGNMENTS.md §1).
    ///
    /// <para>
    /// <b>One batch at issue time, not lazily on first open.</b> 40.21 and 40.22 both rejected the
    /// lazy variant and this block does not reopen it: a progress row's existence has to mean "this
    /// person was asked", so that "who has not started" — the question §5 and roadmap 40.26 are
    /// entirely built on — is a query over rows that exist rather than an inference from rows that
    /// do not. The cost is a transaction that inserts one row and stages one event per recipient,
    /// bounded by <see cref="MaximumFanOutSize"/>.
    /// </para>
    ///
    /// <para>
    /// <b>The roster is read before the transaction opens.</b> Resolving the audience means calling
    /// identity-service, and an HTTP round trip inside a write transaction holds locks on the
    /// progress table for as long as another service takes to answer. The checks below are made
    /// twice — once against the pre-flight read, once against the row inside the transaction —
    /// because between the two a concurrent edit or a double-pressed button could have changed
    /// what is being issued.
    /// </para>
    /// </summary>
    public async Task<AssignmentWriteResult> ActivateAsync(
        Guid assignmentId,
        CancellationToken cancellationToken = default)
    {
        AssignmentAudienceDto audience;

        await using (var previewScope = await TenantTransactionScope.BeginReadAsync(databaseContext, cancellationToken))
        {
            var preview = await databaseContext.Assignments
                .AsNoTracking()
                .FirstOrDefaultAsync(candidate => candidate.Id == assignmentId, cancellationToken);
            if (preview is null)
            {
                return AssignmentWriteResult.NotFound();
            }

            var previewRefusal = DescribeActivationRefusal(preview);
            if (previewRefusal is not null)
            {
                return previewRefusal;
            }

            audience = AssignmentDocumentSerializer.DeserializeAudience(preview.Audience);
        }

        var recipientIds = await ResolveRecipientsAsync(audience, cancellationToken);

        await using var tenantScope = await TenantTransactionScope.BeginWriteAsync(databaseContext, cancellationToken);

        var assignment = await databaseContext.Assignments
            .FirstOrDefaultAsync(candidate => candidate.Id == assignmentId, cancellationToken);
        if (assignment is null)
        {
            return AssignmentWriteResult.NotFound();
        }

        var refusal = DescribeActivationRefusal(assignment);
        if (refusal is not null)
        {
            return refusal;
        }

        var now = DateTime.UtcNow;
        assignment.Status = AssignmentStatuses.Active;
        assignment.ActivatedAt = now;
        assignment.UpdatedAt = now;

        var issuedCount = await AssignmentFanOut.IssueAsync(
            databaseContext, eventPublisher, assignment, recipientIds, cancellationToken);

        await databaseContext.SaveChangesAsync(cancellationToken);
        await tenantScope.CommitAsync(cancellationToken);

        logger.LogInformation(
            "Assignment issued AssignmentId={AssignmentId} Recipients={RecipientCount} Deadline={Deadline}",
            assignment.Id, issuedCount, assignment.Deadline);

        return AssignmentWriteResult.Applied(ToDto(assignment));
    }

    /// <summary>
    /// Phase 40.23. The РОП asking, by hand, for everybody who has not finished to be nudged
    /// (docs/TENANCY/ASSIGNMENTS.md §5: "not a report the РОП might open, but a notification …
    /// with a one-click reminder").
    ///
    /// <para>
    /// <c>failed_threshold</c> rows are reminded along with the rest, and that is the point rather
    /// than an oversight: somebody who tried four times and stayed under the bar is the person the
    /// РОП most needs to reach, and 40.22 separated that state from "not started" precisely so they
    /// would not be lost among people who never opened it.
    /// </para>
    /// </summary>
    public async Task<AssignmentReminderResultDto?> RemindAsync(
        Guid assignmentId,
        CancellationToken cancellationToken = default)
    {
        await using var tenantScope = await TenantTransactionScope.BeginWriteAsync(databaseContext, cancellationToken);

        var assignment = await databaseContext.Assignments
            .FirstOrDefaultAsync(candidate => candidate.Id == assignmentId, cancellationToken);
        if (assignment is null)
        {
            return null;
        }

        if (assignment.Status != AssignmentStatuses.Active)
        {
            throw new AssignmentValidationException(
                $"Only an active assignment can be reminded about; this one is {assignment.Status}.");
        }

        var unfinishedUserIds = await databaseContext.AssignmentProgressRecords
            .AsNoTracking()
            .Where(record => record.AssignmentId == assignmentId
                             && record.Status != AssignmentProgressStatuses.Completed)
            .Select(record => record.UserId)
            .ToListAsync(cancellationToken);

        // One instant for the whole press, so every reminder from this click shares a dedupe key
        // suffix and a redelivery of any of them collapses onto the original.
        var requestedAt = DateTime.UtcNow;

        foreach (var userId in unfinishedUserIds)
        {
            await eventPublisher.PublishAssignmentReminderAsync(
                new AssignmentReminderEvent(
                    assignment.Id, userId, assignment.Title, assignment.Deadline, requestedAt),
                cancellationToken);
        }

        await databaseContext.SaveChangesAsync(cancellationToken);
        await tenantScope.CommitAsync(cancellationToken);

        logger.LogInformation(
            "Assignment reminder sent AssignmentId={AssignmentId} Recipients={RecipientCount}",
            assignment.Id, unfinishedUserIds.Count);

        return new AssignmentReminderResultDto(unfinishedUserIds.Count);
    }

    public async Task<AssignmentWriteResult> CloseAsync(
        Guid assignmentId,
        CancellationToken cancellationToken = default)
    {
        await using var tenantScope = await TenantTransactionScope.BeginWriteAsync(databaseContext, cancellationToken);

        var assignment = await databaseContext.Assignments
            .FirstOrDefaultAsync(candidate => candidate.Id == assignmentId, cancellationToken);
        if (assignment is null)
        {
            return AssignmentWriteResult.NotFound();
        }

        if (assignment.Status != AssignmentStatuses.Active)
        {
            return AssignmentWriteResult.RejectedByStatus(
                $"Only an active assignment can be closed; this one is {assignment.Status}.");
        }

        var now = DateTime.UtcNow;
        assignment.Status = AssignmentStatuses.Closed;
        assignment.ClosedAt = now;
        assignment.UpdatedAt = now;

        await databaseContext.SaveChangesAsync(cancellationToken);
        await tenantScope.CommitAsync(cancellationToken);

        logger.LogInformation("Assignment closed AssignmentId={AssignmentId}", assignment.Id);

        return AssignmentWriteResult.Applied(ToDto(assignment));
    }

    public async Task<AssignmentWriteResult> DeleteAsync(
        Guid assignmentId,
        CancellationToken cancellationToken = default)
    {
        await using var tenantScope = await TenantTransactionScope.BeginWriteAsync(databaseContext, cancellationToken);

        var assignment = await databaseContext.Assignments
            .FirstOrDefaultAsync(candidate => candidate.Id == assignmentId, cancellationToken);
        if (assignment is null)
        {
            return AssignmentWriteResult.NotFound();
        }

        if (assignment.Status != AssignmentStatuses.Draft)
        {
            return AssignmentWriteResult.RejectedByStatus(
                "An assignment that has been issued cannot be deleted. Close it instead.");
        }

        databaseContext.Assignments.Remove(assignment);
        await databaseContext.SaveChangesAsync(cancellationToken);
        await tenantScope.CommitAsync(cancellationToken);

        return AssignmentWriteResult.Applied();
    }

    public async Task<IReadOnlyList<AssignmentProgressDto>?> GetProgressAsync(
        Guid assignmentId,
        CancellationToken cancellationToken = default)
    {
        await using var tenantScope = await TenantTransactionScope.BeginReadAsync(databaseContext, cancellationToken);

        var assignmentExists = await databaseContext.Assignments
            .AsNoTracking()
            .AnyAsync(candidate => candidate.Id == assignmentId, cancellationToken);
        if (!assignmentExists)
        {
            return null;
        }

        return await databaseContext.AssignmentProgressRecords
            .AsNoTracking()
            .Where(record => record.AssignmentId == assignmentId)
            .OrderBy(record => record.Status)
            .ThenBy(record => record.UserId)
            .Select(record => new AssignmentProgressDto(
                record.UserId,
                record.Status,
                record.BestScore,
                record.AttemptCount,
                record.FirstOpenedAt,
                record.CompletedAt))
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Phase 40.23. The status an assignment currently has, or <see langword="null"/> when there is
    /// no such assignment in the caller's organization. Read on its own so an update can decide
    /// whether it will need the roster before it opens a write transaction.
    /// </summary>
    private async Task<string?> ReadStatusAsync(Guid assignmentId, CancellationToken cancellationToken)
    {
        await using var tenantScope = await TenantTransactionScope.BeginReadAsync(databaseContext, cancellationToken);

        return await databaseContext.Assignments
            .AsNoTracking()
            .Where(candidate => candidate.Id == assignmentId)
            .Select(candidate => candidate.Status)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>
    /// Phase 40.23. Resolves the audience rule and refuses a fan-out larger than the transaction
    /// should carry.
    /// </summary>
    private async Task<IReadOnlyList<Guid>> ResolveRecipientsAsync(
        AssignmentAudienceDto audience,
        CancellationToken cancellationToken)
    {
        var recipientIds = await audienceResolver.ResolveAsync(audience, cancellationToken);

        if (recipientIds.Count > AssignmentFanOut.MaximumFanOutSize)
        {
            throw new AssignmentValidationException(
                $"This audience resolves to {recipientIds.Count} people, more than the "
                + $"{AssignmentFanOut.MaximumFanOutSize} one assignment can be issued to at once. Split it.");
        }

        return recipientIds;
    }

    /// <summary>
    /// Phase 40.23. Everything that stops an assignment from being issued, in one place because it
    /// is asked twice — once before the roster lookup and once inside the write transaction.
    /// </summary>
    private static AssignmentWriteResult? DescribeActivationRefusal(Assignment assignment)
    {
        if (assignment.Status != AssignmentStatuses.Draft)
        {
            return AssignmentWriteResult.RejectedByStatus(
                $"Only a draft can be issued; this assignment is {assignment.Status}.");
        }

        var content = AssignmentDocumentSerializer.DeserializeContent(assignment.Content);
        if (content.Count == 0)
        {
            return AssignmentWriteResult.RejectedByStatus(
                "An assignment with no content asks people to do nothing. Add exercises, a dialogue or theory first.");
        }

        // Phase 40.22. The last moment the rule and the content can still be reconciled: issuing
        // freezes both, so a rule that measures something the assignment does not ask for produces
        // an assignment nobody can ever complete — and on the РОП's screen that is indistinguishable
        // from a team that has not started. Refused here, where the administrator can still fix it.
        return DescribeUnmeasurableRule(assignment.CompletionRule, content) is { } unmeasurable
            ? AssignmentWriteResult.RejectedByStatus(unmeasurable)
            : null;
    }

    /// <summary>
    /// Phase 40.22. Returns the refusal to show, or <see langword="null"/> when the rule and the
    /// content agree. A rule that cannot be read at all is deliberately <b>not</b> refused here: it
    /// cannot exist, because <see cref="AssignmentDocumentSerializer.SerializeCompletionRule"/>
    /// parses every rule on the way in, and treating a hypothetical unreadable one as a blocker
    /// would strand an assignment an administrator can no longer edit out of a draft.
    /// </summary>
    private static string? DescribeUnmeasurableRule(
        string completionRule,
        IReadOnlyList<AssignmentContentItemDto> content)
    {
        var rule = AssignmentCompletionRuleReader.TryRead(completionRule);
        if (rule is null)
        {
            return null;
        }

        var requiredKind = rule.Kind switch
        {
            AssignmentCompletionRuleKinds.DialogScore => AssignmentContentItemKinds.DialogScenario,
            AssignmentCompletionRuleKinds.ExerciseAccuracy => AssignmentContentItemKinds.LessonVersion,
            _ => null,
        };

        if (requiredKind is null || content.Any(item => item.Kind == requiredKind))
        {
            return null;
        }

        return $"The completion rule '{rule.Kind}' is measured over '{requiredKind}' content, "
               + "and this assignment has none. Add it, or change the rule — an issued assignment "
               + "cannot have either changed afterwards.";
    }

    private static List<string> CollectFrozenFieldChanges(
        Assignment assignment,
        string sourceType,
        string? sourceRef,
        string content,
        string completionRule)
    {
        var changedFields = new List<string>();

        if (assignment.SourceType != sourceType) changedFields.Add("sourceType");
        if (assignment.SourceRef != sourceRef) changedFields.Add("sourceRef");

        if (AssignmentDocumentSerializer.Canonicalize(assignment.Content)
            != AssignmentDocumentSerializer.Canonicalize(content))
        {
            changedFields.Add("content");
        }

        if (AssignmentDocumentSerializer.Canonicalize(assignment.CompletionRule)
            != AssignmentDocumentSerializer.Canonicalize(completionRule))
        {
            changedFields.Add("completionRule");
        }

        return changedFields;
    }

    private static AssignmentDto ToDto(Assignment assignment)
        => new(
            assignment.Id,
            assignment.Title,
            assignment.Goal,
            assignment.SourceType,
            assignment.SourceRef,
            AssignmentDocumentSerializer.DeserializeContent(assignment.Content),
            AssignmentDocumentSerializer.DeserializeAudience(assignment.Audience),
            assignment.OpensAt,
            assignment.Deadline,
            AssignmentDocumentSerializer.DeserializeRule(assignment.CompletionRule),
            AssignmentDocumentSerializer.DeserializeOptionalRule(assignment.RepeatSchedule),
            assignment.RepeatOfAssignmentId,
            assignment.RepeatWaveIndex,
            assignment.Status,
            assignment.CreatedBy,
            assignment.CreatedAt,
            assignment.UpdatedAt,
            assignment.ActivatedAt,
            assignment.ClosedAt);

    private static string RequireTitle(string? title)
    {
        var normalizedTitle = (title ?? string.Empty).Trim();
        if (normalizedTitle.Length == 0
            || normalizedTitle.Length > AssignmentDocumentSerializer.MaximumTitleLength)
        {
            throw new AssignmentValidationException(
                $"An assignment needs a title of 1 to {AssignmentDocumentSerializer.MaximumTitleLength} characters.");
        }

        return normalizedTitle;
    }

    private static string? NormalizeGoal(string? goal)
    {
        var normalizedGoal = (goal ?? string.Empty).Trim();
        if (normalizedGoal.Length == 0)
        {
            return null;
        }

        if (normalizedGoal.Length > AssignmentDocumentSerializer.MaximumGoalLength)
        {
            throw new AssignmentValidationException(
                $"An assignment goal may hold at most {AssignmentDocumentSerializer.MaximumGoalLength} characters.");
        }

        return normalizedGoal;
    }

    private static string RequireSourceType(string? sourceType)
    {
        var normalizedSourceType = (sourceType ?? string.Empty).Trim();
        if (!AssignmentSourceTypes.IsKnown(normalizedSourceType))
        {
            throw new AssignmentValidationException(
                $"'{sourceType}' is not a known assignment source type.");
        }

        return normalizedSourceType;
    }

    private static string? NormalizeSourceRef(string sourceType, string? sourceRef)
    {
        var normalizedSourceRef = (sourceRef ?? string.Empty).Trim();
        if (normalizedSourceRef.Length == 0)
        {
            return null;
        }

        if (sourceType == AssignmentSourceTypes.Manual)
        {
            throw new AssignmentValidationException(
                "A manual assignment has no source to reference.");
        }

        if (normalizedSourceRef.Length > AssignmentDocumentSerializer.MaximumSourceRefLength)
        {
            throw new AssignmentValidationException(
                $"A source reference may hold at most {AssignmentDocumentSerializer.MaximumSourceRefLength} characters.");
        }

        return normalizedSourceRef;
    }

    private static void RequireConsistentSchedule(DateTime? opensAt, DateTime? deadline)
    {
        if (opensAt is not null && deadline is not null && deadline <= opensAt)
        {
            throw new AssignmentValidationException("The deadline must come after the opening time.");
        }
    }
}
