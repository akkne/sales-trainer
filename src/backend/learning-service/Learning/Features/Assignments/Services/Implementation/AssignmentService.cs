using Microsoft.EntityFrameworkCore;
using Sellevate.Learning.Common.Constants;
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
/// <b>What is not here, and is not an oversight.</b> Nothing evaluates the completion rule (40.22),
/// nothing resolves the audience into people, writes their progress rows or notifies them (40.23),
/// and nothing acts on the repeat schedule (40.24). Consequently <c>AssignmentProgressRecords</c> has
/// no writer in this block and every funnel count reads zero — the honest answer, recorded in
/// docs/DONT_FORGET.md rather than papered over with a lazily-created row on first open.
/// </para>
/// </summary>
internal sealed class AssignmentService(
    LearningDbContext databaseContext,
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
            CompletionRule = AssignmentDocumentSerializer.SerializeRequiredRule(
                requestDto.CompletionRule, "completionRule"),
            RepeatSchedule = AssignmentDocumentSerializer.SerializeOptionalRule(
                requestDto.RepeatSchedule, "repeatSchedule"),
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
        var completionRule = AssignmentDocumentSerializer.SerializeRequiredRule(
            requestDto.CompletionRule, "completionRule");
        var repeatSchedule = AssignmentDocumentSerializer.SerializeOptionalRule(
            requestDto.RepeatSchedule, "repeatSchedule");

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
        assignment.OpensAt = requestDto.OpensAt;
        assignment.Deadline = requestDto.Deadline;
        assignment.CompletionRule = completionRule;
        assignment.RepeatSchedule = repeatSchedule;
        assignment.UpdatedAt = DateTime.UtcNow;

        await databaseContext.SaveChangesAsync(cancellationToken);
        await tenantScope.CommitAsync(cancellationToken);

        return AssignmentWriteResult.Applied(ToDto(assignment));
    }

    public async Task<AssignmentWriteResult> ActivateAsync(
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
                $"Only a draft can be issued; this assignment is {assignment.Status}.");
        }

        if (AssignmentDocumentSerializer.DeserializeContent(assignment.Content).Count == 0)
        {
            return AssignmentWriteResult.RejectedByStatus(
                "An assignment with no content asks people to do nothing. Add exercises, a dialogue or theory first.");
        }

        var now = DateTime.UtcNow;
        assignment.Status = AssignmentStatuses.Active;
        assignment.ActivatedAt = now;
        assignment.UpdatedAt = now;

        await databaseContext.SaveChangesAsync(cancellationToken);
        await tenantScope.CommitAsync(cancellationToken);

        logger.LogInformation(
            "Assignment issued AssignmentId={AssignmentId} Deadline={Deadline}",
            assignment.Id, assignment.Deadline);

        return AssignmentWriteResult.Applied(ToDto(assignment));
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
