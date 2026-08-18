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
    public const int MaximumMaterialLength = 60000;
    public const int MaximumTitleLength = 200;

    /// <summary>Phase 40.31. The width of <c>Assignments.SourceRef</c>, which is where it ends up.</summary>
    public const int MaximumGapSourceRefLength = 200;

    /// <summary>
    /// Lists the organization's runs.
    ///
    /// <para>
    /// Projected in two steps rather than one because the refusal is a <c>jsonb</c> document, and
    /// deserialising it is not something the provider can translate into SQL. The columns pulled are
    /// still only the summary's — the material and the structure stay in the database.
    /// </para>
    /// </summary>
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

        var rows = await query
            .OrderByDescending(job => job.CreatedAt)
            .Select(job => new
            {
                job.Id,
                job.Title,
                job.Status,
                job.GapSourceRef,
                job.Insufficiency,
                job.ProducedLessonId,
                job.ProducedExerciseCount,
                job.FailureReason,
                job.CreatedAt,
                job.UpdatedAt
            })
            .ToListAsync(cancellationToken);

        return rows
            .Select(row => new ContentGenerationJobSummaryDto(
                row.Id,
                row.Title,
                row.Status,
                row.GapSourceRef,
                ContentInsufficiencyDocumentSerializer.Deserialize(row.Insufficiency),
                row.ProducedLessonId,
                row.ProducedExerciseCount,
                row.FailureReason,
                row.CreatedAt,
                row.UpdatedAt))
            .ToList();
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

    /// <summary>
    /// Opens a run from pasted material and hands it to the worker to structure.
    ///
    /// <para>
    /// <b>An empty textarea is the one input that stays a 400.</b> There is nothing to refuse usefully
    /// — «добавьте материал» is what the empty field already says — and
    /// <c>CK_ContentGenerationJobs_Input</c> would refuse the row anyway. Everything above empty and
    /// below the threshold becomes a run in the <c>insufficient</c> state instead, because that
    /// refusal has something to say and something to be argued with.
    /// </para>
    ///
    /// <para>
    /// Stage one of the Phase 40.28 threshold runs here: the free half, before a single token is paid
    /// for.
    /// </para>
    ///
    /// <para>
    /// A gap provenance string that is not one of ours is <b>refused rather than truncated</b>
    /// (Phase 40.31). A reference nobody can parse back to a stage is worse than none, because the
    /// panel would then neither suppress on it nor be able to say why.
    /// </para>
    /// </summary>
    public async Task<ContentGenerationJobDto> StartAsync(
        StartContentGenerationRequestDto request,
        Guid? actorId,
        string? gapSourceRef = null,
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

        if (material.Length == 0)
        {
            throw new ContentGenerationValidationException("material is required.");
        }

        if (material.Length > MaximumMaterialLength)
        {
            throw new ContentGenerationValidationException(
                $"material must be at most {MaximumMaterialLength} characters.");
        }

        var insufficiency = ContentSufficiencyInspector.InspectMaterial(material);

        await using var tenantScope = await TenantTransactionScope.BeginWriteAsync(databaseContext, cancellationToken);

        var now = DateTime.UtcNow;
        var job = new ContentGenerationJob
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            CreatedBy = actorId,
            Title = title,
            SourceMaterial = material,
            GapSourceRef = NormalizeGapSourceRef(gapSourceRef),
            Status = insufficiency is null
                ? ContentGenerationJobStatuses.Structuring
                : ContentGenerationJobStatuses.Insufficient,
            Insufficiency = insufficiency is null
                ? null
                : ContentInsufficiencyDocumentSerializer.Serialize(insufficiency),
            CreatedAt = now,
            UpdatedAt = now
        };

        databaseContext.ContentGenerationJobs.Add(job);
        await databaseContext.SaveChangesAsync(cancellationToken);
        await tenantScope.CommitAsync(cancellationToken);

        logger.LogInformation(
            "Content generation run started JobId={JobId} OrganizationId={OrganizationId} MaterialLength={MaterialLength} Status={Status}",
            job.Id, organizationId, material.Length, job.Status);

        return ToDto(job);
    }

    /// <summary>
    /// «Вот ещё материал» — the answer to a refusal, and the reason the refusal is a state rather than
    /// an error.
    ///
    /// <para>
    /// The text is appended and the run resumes structuring. What it does <b>not</b> do is start over:
    /// <c>StructuredMaterialLength</c> already records how much of the material was read, and the
    /// worker sends only the part after it, alongside the structure it already has. So a РОП who was
    /// told «нет ни одного возражения» pastes their objections list and pays for reading the
    /// objections list — not for reading the fifty-page deck a second time.
    /// </para>
    ///
    /// <para>
    /// <b>Only a refused run takes more material, and only a refused run needs to.</b> Adding to a run
    /// that is mid-call would change the text under a claim already in flight; adding to a completed
    /// one would describe a lesson generated from something else; and a <i>failed</i> run has its own
    /// door — <c>retry</c> — which resumes the half that failed instead of re-reading anything. One
    /// state, one way out of it.
    /// </para>
    ///
    /// <para>
    /// The free check runs again over the whole thing, so a run refused as off-topic that now carries
    /// a sales script is not sent to a model just to be told the same. A run that is still too thin is
    /// refused again here, for nothing.
    /// </para>
    /// </summary>
    public async Task<ContentGenerationJobDto?> SupplementMaterialAsync(
        Guid jobId,
        SupplementContentMaterialRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var addition = (request.Material ?? string.Empty).Trim();
        if (addition.Length == 0)
        {
            throw new ContentGenerationValidationException("material is required.");
        }

        await using var tenantScope = await TenantTransactionScope.BeginWriteAsync(databaseContext, cancellationToken);

        var job = await databaseContext.ContentGenerationJobs
            .FirstOrDefaultAsync(candidate => candidate.Id == jobId, cancellationToken);
        if (job is null)
        {
            return null;
        }

        if (job.Status != ContentGenerationJobStatuses.Insufficient)
        {
            throw new ContentGenerationStateException(
                $"Material can only be added to a run that was refused for thin material (it is '{job.Status}').");
        }

        var material = $"{job.SourceMaterial}\n\n{addition}";
        if (material.Length > MaximumMaterialLength)
        {
            throw new ContentGenerationValidationException(
                $"material must be at most {MaximumMaterialLength} characters.");
        }

        var now = DateTime.UtcNow;
        job.SourceMaterial = material;
        job.UpdatedAt = now;

        var insufficiency = ContentSufficiencyInspector.InspectMaterial(material);
        if (insufficiency is not null)
        {
            job.Status = ContentGenerationJobStatuses.Insufficient;
            job.Insufficiency = ContentInsufficiencyDocumentSerializer.Serialize(insufficiency);

            await databaseContext.SaveChangesAsync(cancellationToken);
            await tenantScope.CommitAsync(cancellationToken);

            return ToDto(job);
        }

        job.Status = ContentGenerationJobStatuses.Structuring;
        job.Insufficiency = null;
        job.FailureReason = null;
        job.Attempts = 0;
        job.ClaimedAt = null;

        await databaseContext.SaveChangesAsync(cancellationToken);
        await tenantScope.CommitAsync(cancellationToken);

        logger.LogInformation(
            "Content generation run resumed with added material JobId={JobId} AddedLength={AddedLength} AlreadyStructured={AlreadyStructured}",
            jobId, addition.Length, job.StructuredMaterialLength);

        return ToDto(job);
    }

    /// <summary>
    /// Accepts a human edit of the structured material, and re-decides the run's state from it.
    ///
    /// <para>
    /// <b>A refused run is editable here as well</b> (Phase 40.28), because a refusal has to be
    /// arguable by somebody who knows the answer: a sales lead told «нет ни одного возражения» may
    /// simply know their four objections and type them, which is a better outcome than making them
    /// find a document that contains them. What they cannot do is argue the threshold away — the
    /// edited structure is re-inspected, and an edit that leaves it just as empty leaves the run
    /// refused.
    /// </para>
    ///
    /// <para>
    /// Only the deterministic half of the inspection runs on an edit. The model's verdict was about
    /// the material, and the human has just overruled the material with knowledge the material did
    /// not contain; paying for a second opinion on their own typing would be both expensive and rude.
    /// </para>
    ///
    /// <para>
    /// <b>The edit decides the state in both directions.</b> Upward, a refused run that has been
    /// filled in returns to the checkpoint. Downward, a checkpoint run edited <i>to</i> emptiness is
    /// refused now rather than at approval — which is what stops it looking ready right up until the
    /// button does nothing.
    /// </para>
    /// </summary>
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

        if (job.Status is not (ContentGenerationJobStatuses.AwaitingReview
            or ContentGenerationJobStatuses.Insufficient))
        {
            throw new ContentGenerationStateException(
                $"The structure can only be edited while the run is awaiting review or refused (it is '{job.Status}').");
        }

        job.Structure = ContentStructureDocumentSerializer.Serialize(structure);
        job.UpdatedAt = DateTime.UtcNow;

        var insufficiency = ContentSufficiencyInspector.InspectStructure(
            ContentStructureDocumentSerializer.Deserialize(job.Structure));

        job.Status = insufficiency is null
            ? ContentGenerationJobStatuses.AwaitingReview
            : ContentGenerationJobStatuses.Insufficient;

        job.Insufficiency = insufficiency is null
            ? null
            : ContentInsufficiencyDocumentSerializer.Serialize(insufficiency);

        await databaseContext.SaveChangesAsync(cancellationToken);
        await tenantScope.CommitAsync(cancellationToken);

        return ToDto(job);
    }

    /// <summary>
    /// Passes the checkpoint: the human has read the structure and asks for the lesson.
    ///
    /// <para>
    /// <b>A run already past the door is returned, not thrown at.</b> That is what makes the button
    /// safe to press twice and the request safe to retry; re-queueing here is what would buy a second
    /// lesson.
    /// </para>
    ///
    /// <para>
    /// <b>The structure is re-inspected at the moment of approval</b> rather than trusted to have been
    /// inspected when it was written — the last gate, and the one a race cannot skip. Between an edit
    /// and an approval there is a network, a second tab and a stale screen. A refusal found here is
    /// recorded first and thrown second, so the screen polling the run and the caller that pressed the
    /// button get the same list, and the run does not sit at a checkpoint it cannot pass.
    /// </para>
    ///
    /// <para>
    /// A fresh half gets a fresh budget: the attempts spent structuring say nothing about whether
    /// generation will succeed, and the lease has to be free for a worker to pick the run up.
    /// </para>
    /// </summary>
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
        var insufficiency = ContentSufficiencyInspector.InspectStructure(structure);
        if (insufficiency is not null)
        {
            job.Status = ContentGenerationJobStatuses.Insufficient;
            job.Insufficiency = ContentInsufficiencyDocumentSerializer.Serialize(insufficiency);
            job.UpdatedAt = DateTime.UtcNow;

            await databaseContext.SaveChangesAsync(cancellationToken);
            await tenantScope.CommitAsync(cancellationToken);

            throw new ContentGenerationInsufficientMaterialException(
                "There is not enough in this structure to generate exercises worth having.",
                insufficiency);
        }

        var now = DateTime.UtcNow;
        job.Status = ContentGenerationJobStatuses.Generating;
        job.Insufficiency = null;
        job.ApprovedAt = now;
        job.ApprovedBy = actorId;
        job.UpdatedAt = now;

        job.Attempts = 0;
        job.ClaimedAt = null;
        job.FailureReason = null;

        await databaseContext.SaveChangesAsync(cancellationToken);
        await tenantScope.CommitAsync(cancellationToken);

        logger.LogInformation("Content generation run approved JobId={JobId} ActorId={ActorId}", jobId, actorId);

        return ToDto(job);
    }

    /// <summary>
    /// Resumes a failed run.
    ///
    /// <para>
    /// <b>Resumes the half that failed, never the whole pipeline.</b> Both halves are paid for in
    /// tokens, and a retry that silently re-buys a half that already succeeded is a bill nobody can
    /// explain. A run that failed while generating goes back to generating rather than back to the
    /// checkpoint, because the human already answered that question.
    /// </para>
    /// </summary>
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

    /// <summary>
    /// Phase 40.31. A gap reference is either in the <c>skill-gap:</c> namespace and short enough to
    /// be copied into <c>Assignments.SourceRef</c>, or it is not recorded at all.
    /// </summary>
    private static string? NormalizeGapSourceRef(string? gapSourceRef)
    {
        var normalizedGapSourceRef = (gapSourceRef ?? string.Empty).Trim();

        if (normalizedGapSourceRef.Length == 0)
        {
            return null;
        }

        if (!SkillGapSourceRefs.IsSkillGapReference(normalizedGapSourceRef)
            || normalizedGapSourceRef.Length > MaximumGapSourceRefLength)
        {
            throw new ContentGenerationValidationException(
                $"'{gapSourceRef}' is not a usable gap reference.");
        }

        return normalizedGapSourceRef;
    }

    internal static ContentGenerationJobDto ToDto(ContentGenerationJob job) => new(
        job.Id,
        job.Title,
        job.Status,
        job.GapSourceRef,
        job.SourceMaterial,
        job.Structure is null ? null : ContentStructureDocumentSerializer.Deserialize(job.Structure),
        ContentInsufficiencyDocumentSerializer.Deserialize(job.Insufficiency),
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
