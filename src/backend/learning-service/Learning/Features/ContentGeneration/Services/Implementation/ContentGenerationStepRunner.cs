using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Sellevate.BuildingBlocks.Tenancy;
using Sellevate.Learning.Common.Constants;
using Sellevate.Learning.Features.Content.Services.Abstract;
using Sellevate.Learning.Features.ContentGeneration.Models;
using Sellevate.Learning.Features.ContentGeneration.Services.Abstract;
using Sellevate.Learning.Features.Exercises.Services;
using Sellevate.Learning.Features.Lessons.Models;
using Sellevate.Learning.Features.Lessons.Services.Abstract;
using Sellevate.Learning.Features.Lessons.Services.Implementation;
using Sellevate.Learning.Features.SkillTree.Models;
using Sellevate.Learning.Infrastructure.Ai;
using Sellevate.Learning.Infrastructure.Data;

namespace Sellevate.Learning.Features.ContentGeneration.Services.Implementation;

/// <summary>
/// Phase 40.27. The two LLM halves of the pipeline as seen from learning-service: claim a run, make
/// one call, write what came back.
///
/// <para>
/// <b>The claim is committed before the call, and that is the whole concurrency design.</b> A
/// generation call takes minutes and cannot be held open inside a database transaction — an
/// idle-in-transaction connection for five minutes is a real operational problem, and a rollback
/// would not un-bill the provider anyway. So the run is stamped and committed, the call is made
/// outside any transaction, and a second transaction records the result. A process that dies in
/// between leaves a claimed run that the lease releases, and the attempt it consumed is already on
/// the row — which is what stops a crash loop from buying a lesson per restart.
/// </para>
///
/// <para>
/// <b>Nothing here decides the tenant.</b> The caller opened the scope and set the organization; every
/// query below is filtered by the query filter and the row-level-security policy exactly as a request
/// would be. The generated content is written with that organization as its owner and is never
/// global — the shared library has exactly one authoring path and it is the seeder (docs/SEEDER.md §0).
/// </para>
/// </summary>
internal sealed class ContentGenerationStepRunner(
    LearningDbContext databaseContext,
    ITenantContext tenantContext,
    IAiContentPipelineClient aiContentPipelineClient,
    IOrganizationProfileProvider organizationProfileProvider,
    ILessonVersionService lessonVersionService,
    IOptions<ContentGenerationOptions> options,
    ILogger<ContentGenerationStepRunner> logger) : IContentGenerationStepRunner
{
    private const int GeneratedSkillOrderInTree = 900;
    private const int MaximumFailureReasonLength = 1000;

    public async Task<int> RunPendingAsync(CancellationToken cancellationToken = default)
    {
        var jobIds = await FindClaimableJobIdsAsync(cancellationToken);
        if (jobIds.Count == 0)
        {
            return 0;
        }

        var advancedCount = 0;

        foreach (var jobId in jobIds)
        {
            var claim = await TryClaimAsync(jobId, cancellationToken);
            if (claim is null)
            {
                continue;
            }

            try
            {
                if (claim.Status == ContentGenerationJobStatuses.Structuring)
                {
                    await RunStructuringAsync(claim, cancellationToken);
                }
                else
                {
                    await RunGenerationAsync(claim, cancellationToken);
                }

                advancedCount++;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "Content generation step {Status} failed for JobId={JobId}",
                    claim.Status, jobId);

                await RecordFailureAsync(jobId, exception.Message, CancellationToken.None);
            }
        }

        return advancedCount;
    }

    private async Task<IReadOnlyList<Guid>> FindClaimableJobIdsAsync(CancellationToken cancellationToken)
    {
        var leaseExpiry = DateTime.UtcNow.AddMinutes(-Math.Clamp(options.Value.ClaimLeaseMinutes, 1, 120));
        var maximumAttempts = options.Value.MaximumAttempts;

        await using var tenantScope = await TenantTransactionScope.BeginReadAsync(databaseContext, cancellationToken);

        return await databaseContext.ContentGenerationJobs
            .AsNoTracking()
            .Where(job => ContentGenerationJobStatuses.WorkerOwned.Contains(job.Status)
                          && job.Attempts < maximumAttempts
                          && (job.ClaimedAt == null || job.ClaimedAt < leaseExpiry))
            .OrderBy(job => job.CreatedAt)
            .Take(Math.Clamp(options.Value.MaximumJobsPerTick, 1, 20))
            .Select(job => job.Id)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Stamps the lease and spends an attempt, in a transaction of its own that commits before any
    /// LLM call is made. Returns null when another tick got there first.
    ///
    /// <para>
    /// <b>A single conditional UPDATE, not read-then-write.</b> Read-modify-save under READ COMMITTED
    /// lets two instances both see a free lease and both stamp it — and both would then pay for the
    /// same generation. The predicate travels inside the UPDATE, so exactly one tick reports a row
    /// and the other gets nothing. It is the one place in this service that writes without the change
    /// tracker, and it is inside the tenant transaction, so the row-level-security policy applies to
    /// it exactly as it would to a tracked save.
    /// </para>
    /// </summary>
    private async Task<ClaimedContentGenerationJob?> TryClaimAsync(Guid jobId, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var leaseExpiry = now.AddMinutes(-Math.Clamp(options.Value.ClaimLeaseMinutes, 1, 120));
        var maximumAttempts = options.Value.MaximumAttempts;

        await using var tenantScope = await TenantTransactionScope.BeginWriteAsync(databaseContext, cancellationToken);

        var claimedRowCount = await databaseContext.ContentGenerationJobs
            .Where(job => job.Id == jobId
                          && ContentGenerationJobStatuses.WorkerOwned.Contains(job.Status)
                          && job.Attempts < maximumAttempts
                          && (job.ClaimedAt == null || job.ClaimedAt < leaseExpiry))
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(job => job.ClaimedAt, now)
                    .SetProperty(job => job.Attempts, job => job.Attempts + 1)
                    .SetProperty(job => job.UpdatedAt, now),
                cancellationToken);

        if (claimedRowCount == 0)
        {
            return null;
        }

        var claimedJob = await databaseContext.ContentGenerationJobs
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == jobId, cancellationToken);

        await tenantScope.CommitAsync(cancellationToken);

        return claimedJob is null
            ? null
            : new ClaimedContentGenerationJob(
                claimedJob.Id,
                claimedJob.Status,
                claimedJob.Title,
                claimedJob.SourceMaterial,
                claimedJob.StructuredMaterialLength,
                claimedJob.Structure,
                claimedJob.ApprovedBy,
                claimedJob.ProducedLessonId);
    }

    private async Task RunStructuringAsync(
        ClaimedContentGenerationJob claim,
        CancellationToken cancellationToken)
    {
        // Phase 40.28. A run that was refused and has since been given more material is resumed, not
        // restarted: only the part of the material nobody has read yet is sent, and what is already
        // known travels as the known structure the prompt is told to keep rather than rewrite. A РОП
        // arguing with «нет ни одного возражения» pays for reading their objections list, not for
        // reading the fifty-page deck a second time.
        var alreadyStructuredLength = Math.Clamp(
            claim.StructuredMaterialLength, 0, claim.SourceMaterial.Length);
        var materialToRead = claim.SourceMaterial[alreadyStructuredLength..];

        // Seeded from the profile so a customer who already filled the form in is not asked the same
        // seven questions again, and so the model is told to fill gaps rather than to contradict a
        // human. An empty profile is sent as nothing at all rather than as an object of nulls. On a
        // resumed run the seed is the run's own structure instead — it already contains the profile's
        // contribution and, more importantly, the reviewer's.
        var knownStructure = claim.Structure is not null
            ? ContentStructureDocumentSerializer.Deserialize(claim.Structure)
            : ContentStructureDto.FromProfile(
                await organizationProfileProvider.GetCurrentAsync(cancellationToken));

        var structured = await aiContentPipelineClient.StructureAsync(
            new AiStructureMaterialRequest(materialToRead, knownStructure.IsEmpty ? null : knownStructure),
            cancellationToken);

        var structure = structured.Structure ?? ContentStructureDto.Empty;

        // Phase 40.28, stage two: the structure is the honest signal. Length said the material was
        // worth reading; what could actually be read out of it says whether four good exercises can
        // be built. The model's opinion arrived in the same completion and can add a refusal here —
        // it is the only judge able to recognise a recipe that happens to mention a price — but it
        // cannot lift one, or a confident completion would be all it took to bypass the threshold.
        var insufficiency = ContentSufficiencyInspector.InspectStructure(structure, structured.Sufficiency);

        await using var tenantScope = await TenantTransactionScope.BeginWriteAsync(databaseContext, cancellationToken);

        var job = await databaseContext.ContentGenerationJobs
            .FirstOrDefaultAsync(candidate => candidate.Id == claim.JobId, cancellationToken);

        // Somebody could have retried or the row could have moved on while the call was in flight.
        // Writing a structure over a state that is no longer structuring would overwrite a reviewer's
        // edit with the model's first draft.
        if (job is null || job.Status != ContentGenerationJobStatuses.Structuring)
        {
            return;
        }

        var now = DateTime.UtcNow;
        job.Structure = ContentStructureDocumentSerializer.Serialize(structure);
        job.StructuredAt = now;

        // Recorded whichever way the verdict went: the material has been read and paid for, and if
        // the run is refused and later resumed, this is what stops it being read again.
        job.StructuredMaterialLength = job.SourceMaterial.Length;

        job.Status = insufficiency is null
            ? ContentGenerationJobStatuses.AwaitingReview
            : ContentGenerationJobStatuses.Insufficient;

        job.Insufficiency = insufficiency is null
            ? null
            : ContentInsufficiencyDocumentSerializer.Serialize(insufficiency);

        job.ClaimedAt = null;
        job.FailureReason = null;
        job.UpdatedAt = now;

        await databaseContext.SaveChangesAsync(cancellationToken);
        await tenantScope.CommitAsync(cancellationToken);

        if (insufficiency is null)
        {
            logger.LogInformation(
                "Content generation run reached the review checkpoint JobId={JobId} Objections={ObjectionCount} ScriptStages={ScriptStageCount}",
                claim.JobId, structure.Objections.Count, structure.ScriptStages.Count);
        }
        else
        {
            // Logged at Information, not Warning: refusing thin material is the feature working, not
            // a fault. A run of these lines against one organization is, however, the signal that
            // their onboarding never told them what to upload.
            logger.LogInformation(
                "Content generation run refused for thin material JobId={JobId} Gaps={Gaps} ModelNote={ModelNote}",
                claim.JobId,
                string.Join(",", insufficiency.Gaps.Select(gap => gap.Code)),
                insufficiency.Note);
        }
    }

    private async Task RunGenerationAsync(
        ClaimedContentGenerationJob claim,
        CancellationToken cancellationToken)
    {
        var structure = ContentStructureDocumentSerializer.Deserialize(claim.Structure);

        var generatedLesson = await aiContentPipelineClient.GenerateAsync(
            new AiGenerateExercisesRequest(
                structure,
                claim.Title,
                Math.Clamp(options.Value.MaximumExercisesPerLesson, 1, 15)),
            cancellationToken);

        await using var tenantScope = await TenantTransactionScope.BeginWriteAsync(databaseContext, cancellationToken);

        var job = await databaseContext.ContentGenerationJobs
            .FirstOrDefaultAsync(candidate => candidate.Id == claim.JobId, cancellationToken);

        // The cost guard, re-read after the call rather than before it. A lease that expired while a
        // long call was in flight lets a second tick pick the run up, and this is where the loser
        // finds out: a run that already holds a lesson id has been paid for, and writing a second
        // lesson would bill the customer twice for one approval. (The pre-call case cannot arise —
        // CK_ContentGenerationJobs_Produced forbids a lesson id outside the completed state.)
        if (job is null || job.Status != ContentGenerationJobStatuses.Generating || job.ProducedLessonId is not null)
        {
            return;
        }

        var lesson = await WriteGeneratedLessonAsync(job, generatedLesson, cancellationToken);
        if (lesson is null)
        {
            // Everything the model returned failed validation. That is a failure, not an empty
            // success: a "completed" run pointing at a lesson with no exercises is the worst of both
            // — it looks finished and teaches nothing.
            throw new InvalidOperationException(
                "No generated exercise passed content validation; nothing was written.");
        }

        var publishResult = await lessonVersionService.PublishAsync(
            lesson.LessonId, isBreaking: false, job.ApprovedBy, cancellationToken);

        var now = DateTime.UtcNow;
        job.ProducedLessonId = lesson.LessonId;
        job.ProducedLessonVersionId = publishResult?.Version.Id;
        job.ProducedExerciseCount = lesson.ExerciseCount;
        job.GeneratedAt = now;
        job.Status = ContentGenerationJobStatuses.Completed;
        job.ClaimedAt = null;
        job.FailureReason = null;
        job.UpdatedAt = now;

        await databaseContext.SaveChangesAsync(cancellationToken);
        await tenantScope.CommitAsync(cancellationToken);

        logger.LogInformation(
            "Content generation run produced a lesson JobId={JobId} LessonId={LessonId} Exercises={ExerciseCount} Rejected={RejectedCount}",
            job.Id, lesson.LessonId, lesson.ExerciseCount, lesson.RejectedCount);
    }

    /// <summary>
    /// Writes the generated lesson into the ordinary content tables, owned by this organization.
    ///
    /// <para>
    /// <b>Ordinary rows, not a parallel store.</b> The lesson, its exercises and the frozen version
    /// are the same three things every other lesson in the product is made of, so the eleven existing
    /// renderers play it, the existing editor edits it, 40.18's override machinery applies to it and
    /// 40.16's progress binding works on it with no new code. A second home for generated content
    /// would have been a second grading path and a second thing for 40.19's substitution to forget.
    /// </para>
    ///
    /// <para>
    /// <b>It lands archived.</b> The checkpoint this block buys is before generation; whether the
    /// generated exercises themselves are good is a second question, and answering it item by item is
    /// roadmap 40.32. Until somebody looks, unreviewed model output must not appear in the team's
    /// live tree — so the lesson is complete, addressable and invisible to learners, and un-archiving
    /// it is the ordinary <c>PUT /admin/lessons/{id}</c>.
    /// </para>
    /// </summary>
    private async Task<GeneratedLessonWriteResult?> WriteGeneratedLessonAsync(
        ContentGenerationJob job,
        AiGeneratedLesson generatedLesson,
        CancellationToken cancellationToken)
    {
        var organizationId = tenantContext.OrganizationId
            ?? throw new InvalidOperationException("Generated content must belong to an organization.");

        var validExercises = new List<AiGeneratedExercise>();
        var rejectedCount = 0;

        foreach (var generatedExercise in generatedLesson.Exercises)
        {
            var validationErrors = ExerciseContentValidator.Validate(
                generatedExercise.Type, generatedExercise.Content);

            if (validationErrors.Count > 0)
            {
                rejectedCount++;
                logger.LogWarning(
                    "Dropped a generated {ExerciseType} exercise for JobId={JobId}: {Errors}",
                    generatedExercise.Type, job.Id, string.Join(" ", validationErrors));
                continue;
            }

            validExercises.Add(generatedExercise);
        }

        if (validExercises.Count == 0)
        {
            return null;
        }

        var skill = await EnsureGeneratedSkillAsync(organizationId, cancellationToken);

        var topic = new Topic
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            SkillId = skill.Id,
            // Derived from the run's own id, so it is unique per organization by construction and
            // never needs a retry loop — the call LessonSlugGenerator makes for slugs, for the same
            // reason.
            IconicName = $"generated-{job.Id:N}",
            Title = job.Title,
            OrderInSkill = await NextTopicOrderAsync(skill.Id, cancellationToken)
        };

        databaseContext.Topics.Add(topic);

        var lessonId = Guid.NewGuid();
        var lesson = new Lesson
        {
            Id = lessonId,
            OrganizationId = organizationId,
            TopicId = topic.Id,
            Title = string.IsNullOrWhiteSpace(generatedLesson.Title) ? job.Title : generatedLesson.Title,
            OrderInTopic = 1,
            Slug = LessonSlugGenerator.GenerateFromLessonId(lessonId),
            IsArchived = true
        };

        databaseContext.Lessons.Add(lesson);

        var now = DateTime.UtcNow;
        var orderInLesson = 1;
        foreach (var generatedExercise in validExercises)
        {
            databaseContext.Exercises.Add(new Exercise
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                LessonId = lessonId,
                Type = generatedExercise.Type,
                OrderInLesson = orderInLesson++,
                SerializedContent = generatedExercise.Content.GetRawText(),
                CreatedAt = now,
                UpdatedAt = now
            });
        }

        await databaseContext.SaveChangesAsync(cancellationToken);

        return new GeneratedLessonWriteResult(lessonId, validExercises.Count, rejectedCount);
    }

    private async Task<Skill> EnsureGeneratedSkillAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        var iconicName = options.Value.GeneratedSkillIconicName;

        var skill = await databaseContext.Skills
            .FirstOrDefaultAsync(
                candidate => candidate.OrganizationId == organizationId && candidate.IconicName == iconicName,
                cancellationToken);

        if (skill is not null)
        {
            return skill;
        }

        skill = new Skill
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            IconicName = iconicName,
            Title = options.Value.GeneratedSkillTitle,
            OrderInTree = GeneratedSkillOrderInTree,
            Stage = "general"
        };

        databaseContext.Skills.Add(skill);
        await databaseContext.SaveChangesAsync(cancellationToken);

        return skill;
    }

    private async Task<int> NextTopicOrderAsync(Guid skillId, CancellationToken cancellationToken)
    {
        var highestOrder = await databaseContext.Topics
            .Where(topic => topic.SkillId == skillId)
            .Select(topic => (int?)topic.OrderInSkill)
            .MaxAsync(cancellationToken);

        return (highestOrder ?? 0) + 1;
    }

    /// <summary>
    /// Records why the attempt failed and, when the budget is spent, hands the run back to a person.
    /// The lease is released either way so the next tick can retry within the budget.
    /// </summary>
    private async Task RecordFailureAsync(Guid jobId, string reason, CancellationToken cancellationToken)
    {
        try
        {
            // The failed step may have left half a lesson in the change tracker after its transaction
            // rolled back. Saving here without clearing would try to insert those rows again, into a
            // run that just failed — a torn lesson nobody asked for, attached to nothing.
            databaseContext.ChangeTracker.Clear();

            await using var tenantScope =
                await TenantTransactionScope.BeginWriteAsync(databaseContext, cancellationToken);

            var job = await databaseContext.ContentGenerationJobs
                .FirstOrDefaultAsync(candidate => candidate.Id == jobId, cancellationToken);
            if (job is null)
            {
                return;
            }

            job.FailureReason = reason.Length <= MaximumFailureReasonLength
                ? reason
                : reason[..MaximumFailureReasonLength];
            job.ClaimedAt = null;
            job.UpdatedAt = DateTime.UtcNow;

            if (job.Attempts >= options.Value.MaximumAttempts)
            {
                job.Status = ContentGenerationJobStatuses.Failed;
            }

            await databaseContext.SaveChangesAsync(cancellationToken);
            await tenantScope.CommitAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            // The failure path failing must not take the sweep down for every other run in the
            // organization; the lease will release this one on its own.
            logger.LogError(exception, "Could not record a content generation failure for JobId={JobId}", jobId);
        }
    }

    /// <summary>A claimed run, detached from the tracker so the long call holds no entity.</summary>
    private sealed record ClaimedContentGenerationJob(
        Guid JobId,
        string Status,
        string Title,
        string SourceMaterial,
        int StructuredMaterialLength,
        string? Structure,
        Guid? ApprovedBy,
        Guid? ProducedLessonId);

    private sealed record GeneratedLessonWriteResult(Guid LessonId, int ExerciseCount, int RejectedCount);
}
