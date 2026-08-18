using Microsoft.EntityFrameworkCore;
using Sellevate.BuildingBlocks.Tenancy;
using Sellevate.Learning.Common.Constants;
using Sellevate.Learning.Features.ContentGeneration.Models;
using Sellevate.Learning.Features.ContentGeneration.Services.Abstract;
using Sellevate.Learning.Infrastructure.Data;

namespace Sellevate.Learning.Features.ContentGeneration.Services.Implementation;

/// <summary>
/// Phase 40.27. The human half of the pipeline's state machine.
/// </summary>
internal sealed class ContentGenerationJobService(
    LearningDbContext databaseContext,
    ITenantContext tenantContext,
    ILogger<ContentGenerationJobService> logger) : IContentGenerationJobService
{
    public const int MinimumMaterialLength = 200;
    public const int MaximumMaterialLength = 60000;
    public const int MaximumTitleLength = 200;

    public async Task<IReadOnlyList<ContentGenerationJobSummaryDto>> GetJobsAsync(
        string? status,
        CancellationToken cancellationToken = default)
    {
        if (status is not null && !ContentGenerationJobStatuses.IsKnown(status))
        {
            throw new ContentGenerationValidationException(
                $"Unknown status '{status}'. Valid values: {string.Join(", ", ContentGenerationJobStatuses.All)}.");
        }

        await using var tenantScope = await TenantTransactionScope.BeginReadAsync(databaseContext, cancellationToken);

        var query = databaseContext.ContentGenerationJobs.AsNoTracking();
        if (status is not null)
        {
            query = query.Where(job => job.Status == status);
        }

        var jobs = await query
            .OrderByDescending(job => job.CreatedAt)
            .Select(job => new ContentGenerationJobSummaryDto(
                job.Id,
                job.Title,
                job.Status,
                job.ProducedLessonId,
                job.ProducedExerciseCount,
                job.FailureReason,
                job.CreatedAt,
                job.UpdatedAt))
            .ToListAsync(cancellationToken);

        return jobs;
    }

    public async Task<ContentGenerationJobDto?> GetJobAsync(
        Guid jobId,
        CancellationToken cancellationToken = default)
    {
        await using var tenantScope = await TenantTransactionScope.BeginReadAsync(databaseContext, cancellationToken);

        var job = await databaseContext.ContentGenerationJobs
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == jobId, cancellationToken);

        return job is null ? null : ToDto(job);
    }

    public async Task<ContentGenerationJobDto> StartAsync(
        StartContentGenerationRequestDto request,
        Guid? actorId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var organizationId = tenantContext.OrganizationId
            ?? throw new ContentGenerationValidationException(
                "A content generation run belongs to exactly one organization.");

        var title = (request.Title ?? string.Empty).Trim();
        if (title.Length == 0 || title.Length > MaximumTitleLength)
        {
            throw new ContentGenerationValidationException(
                $"title is required and must be at most {MaximumTitleLength} characters.");
        }

        var material = (request.Material ?? string.Empty).Trim();

        // The floor is deliberately crude — a length, not a judgement. Refusing thin material with a
        // sentence about what is missing («добавьте примеры возражений или запись звонка») is 40.28,
        // and it needs the model's opinion rather than a character count. What this stops today is
        // the empty-textarea case, which would otherwise buy a structuring call to learn nothing.
        if (material.Length < MinimumMaterialLength)
        {
            throw new ContentGenerationValidationException(
                $"material is required and must be at least {MinimumMaterialLength} characters.");
        }

        if (material.Length > MaximumMaterialLength)
        {
            throw new ContentGenerationValidationException(
                $"material must be at most {MaximumMaterialLength} characters.");
        }

        await using var tenantScope = await TenantTransactionScope.BeginWriteAsync(databaseContext, cancellationToken);

        var now = DateTime.UtcNow;
        var job = new ContentGenerationJob
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            CreatedBy = actorId,
            Title = title,
            SourceMaterial = material,
            Status = ContentGenerationJobStatuses.Structuring,
            CreatedAt = now,
            UpdatedAt = now
        };

        databaseContext.ContentGenerationJobs.Add(job);
        await databaseContext.SaveChangesAsync(cancellationToken);
        await tenantScope.CommitAsync(cancellationToken);

        logger.LogInformation(
            "Content generation run started JobId={JobId} OrganizationId={OrganizationId} MaterialLength={MaterialLength}",
            job.Id, organizationId, material.Length);

        return ToDto(job);
    }

    public async Task<ContentGenerationJobDto?> UpdateStructureAsync(
        Guid jobId,
        ContentStructureDto structure,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(structure);

        await using var tenantScope = await TenantTransactionScope.BeginWriteAsync(databaseContext, cancellationToken);

        var job = await databaseContext.ContentGenerationJobs
            .FirstOrDefaultAsync(candidate => candidate.Id == jobId, cancellationToken);
        if (job is null)
        {
            return null;
        }

        if (job.Status != ContentGenerationJobStatuses.AwaitingReview)
        {
            throw new ContentGenerationStateException(
                $"The structure can only be edited while the run is awaiting review (it is '{job.Status}').");
        }

        job.Structure = ContentStructureDocumentSerializer.Serialize(structure);
        job.UpdatedAt = DateTime.UtcNow;

        await databaseContext.SaveChangesAsync(cancellationToken);
        await tenantScope.CommitAsync(cancellationToken);

        return ToDto(job);
    }

    public async Task<ContentGenerationJobDto?> ApproveAsync(
        Guid jobId,
        Guid? actorId,
        CancellationToken cancellationToken = default)
    {
        await using var tenantScope = await TenantTransactionScope.BeginWriteAsync(databaseContext, cancellationToken);

        var job = await databaseContext.ContentGenerationJobs
            .FirstOrDefaultAsync(candidate => candidate.Id == jobId, cancellationToken);
        if (job is null)
        {
            return null;
        }

        // Already past the door. Returning the run rather than throwing is what makes the button
        // safe to press twice and the request safe to retry; re-queueing here is what would buy a
        // second lesson.
        if (job.Status is ContentGenerationJobStatuses.Generating or ContentGenerationJobStatuses.Completed)
        {
            return ToDto(job);
        }

        if (job.Status != ContentGenerationJobStatuses.AwaitingReview)
        {
            throw new ContentGenerationStateException(
                $"Only a run awaiting review can be approved (it is '{job.Status}').");
        }

        var structure = ContentStructureDocumentSerializer.Deserialize(job.Structure);
        if (structure.IsEmpty)
        {
            throw new ContentGenerationValidationException(
                "There is nothing in the structure to generate from — fill in the product, the client, "
                + "the objections or the script stages first.");
        }

        var now = DateTime.UtcNow;
        job.Status = ContentGenerationJobStatuses.Generating;
        job.ApprovedAt = now;
        job.ApprovedBy = actorId;
        job.UpdatedAt = now;

        // A fresh half gets a fresh budget: the attempts spent structuring say nothing about whether
        // generation will succeed, and the lease has to be free for a worker to pick the run up.
        job.Attempts = 0;
        job.ClaimedAt = null;
        job.FailureReason = null;

        await databaseContext.SaveChangesAsync(cancellationToken);
        await tenantScope.CommitAsync(cancellationToken);

        logger.LogInformation("Content generation run approved JobId={JobId} ActorId={ActorId}", jobId, actorId);

        return ToDto(job);
    }

    public async Task<ContentGenerationJobDto?> RetryAsync(
        Guid jobId,
        CancellationToken cancellationToken = default)
    {
        await using var tenantScope = await TenantTransactionScope.BeginWriteAsync(databaseContext, cancellationToken);

        var job = await databaseContext.ContentGenerationJobs
            .FirstOrDefaultAsync(candidate => candidate.Id == jobId, cancellationToken);
        if (job is null)
        {
            return null;
        }

        if (job.Status != ContentGenerationJobStatuses.Failed)
        {
            throw new ContentGenerationStateException(
                $"Only a failed run can be retried (it is '{job.Status}').");
        }

        if (job.ProducedLessonId is not null)
        {
            throw new ContentGenerationStateException(
                "This run has already produced a lesson; there is nothing left to retry.");
        }

        // Resume the half that failed, never the whole pipeline. Both halves are paid for in tokens,
        // and a "retry" that silently re-buys a half that succeeded is a bill nobody can explain. A
        // run that failed while generating goes back to generating rather than back to the
        // checkpoint, because the human already answered that question.
        job.Status = job.Structure is null
            ? ContentGenerationJobStatuses.Structuring
            : job.ApprovedAt is not null
                ? ContentGenerationJobStatuses.Generating
                : ContentGenerationJobStatuses.AwaitingReview;

        job.Attempts = 0;
        job.ClaimedAt = null;
        job.FailureReason = null;
        job.UpdatedAt = DateTime.UtcNow;

        await databaseContext.SaveChangesAsync(cancellationToken);
        await tenantScope.CommitAsync(cancellationToken);

        logger.LogInformation("Content generation run retried JobId={JobId} ResumedAt={Status}", jobId, job.Status);

        return ToDto(job);
    }

    internal static ContentGenerationJobDto ToDto(ContentGenerationJob job) => new(
        job.Id,
        job.Title,
        job.Status,
        job.SourceMaterial,
        job.Structure is null ? null : ContentStructureDocumentSerializer.Deserialize(job.Structure),
        job.StructuredAt,
        job.ApprovedAt,
        job.ProducedLessonId,
        job.ProducedLessonVersionId,
        job.ProducedExerciseCount,
        job.GeneratedAt,
        job.FailureReason,
        job.CreatedAt,
        job.UpdatedAt);
}
