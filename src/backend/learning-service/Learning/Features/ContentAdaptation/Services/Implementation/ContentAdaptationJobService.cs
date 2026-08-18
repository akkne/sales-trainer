using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Sellevate.BuildingBlocks.Tenancy;
using Sellevate.Learning.Common.Constants;
using Sellevate.Learning.Features.Content;
using Sellevate.Learning.Features.Content.Models;
using Sellevate.Learning.Features.Content.Services.Abstract;
using Sellevate.Learning.Features.ContentAdaptation.Models;
using Sellevate.Learning.Features.ContentAdaptation.Services.Abstract;
using Sellevate.Learning.Features.Exercises.Services;
using Sellevate.Learning.Features.Lessons.Models;
using Sellevate.Learning.Infrastructure.Data;

namespace Sellevate.Learning.Features.ContentAdaptation.Services.Implementation;

/// <summary>
/// Phase 40.32. The human half of batch adaptation — and the only place in the block where a
/// proposal can reach live content.
///
/// <para>
/// <b>Applying is a request, never a tick.</b> The worker writes rows in
/// <c>ContentAdaptationItems</c> and has no branch that touches an <c>Exercise</c>; this class has
/// exactly one, and it runs inside an organization administrator's HTTP request with their id on it.
/// That is the roadmap's «никогда не автоприменение» as a property of the code's shape rather than
/// as a rule somebody has to keep remembering.
/// </para>
/// </summary>
internal sealed class ContentAdaptationJobService(
    LearningDbContext databaseContext,
    ITenantContext tenantContext,
    IContentOverrideService contentOverrideService,
    IOptions<ContentAdaptationOptions> options,
    ILogger<ContentAdaptationJobService> logger) : IContentAdaptationJobService
{
    public const int MaximumStageKeyLength = 64;
    private const int MaximumLessonTitleLength = 300;

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyList<ContentAdaptationJobSummaryDto>> GetJobsAsync(
        string? mode,
        string? status,
        CancellationToken cancellationToken = default)
    {
        if (mode is not null && !ContentAdaptationModes.IsKnown(mode))
        {
            throw new ContentAdaptationValidationException(
                $"Unknown mode '{mode}'. Valid values: {string.Join(", ", ContentAdaptationModes.All)}.");
        }

        if (status is not null && !ContentAdaptationStatuses.IsKnown(status))
        {
            throw new ContentAdaptationValidationException(
                $"Unknown status '{status}'. Valid values: {string.Join(", ", ContentAdaptationStatuses.All)}.");
        }

        await using var tenantScope = await TenantTransactionScope.BeginReadAsync(databaseContext, cancellationToken);

        var query = databaseContext.ContentAdaptationJobs.AsNoTracking();
        if (mode is not null)
        {
            query = query.Where(job => job.Mode == mode);
        }

        if (status is not null)
        {
            query = query.Where(job => job.Status == status);
        }

        var jobs = await query
            .OrderByDescending(job => job.CreatedAt)
            .ToListAsync(cancellationToken);

        if (jobs.Count == 0)
        {
            return [];
        }

        var jobIds = jobs.Select(job => job.Id).ToList();

        // One grouped count for the whole page rather than one query per batch: the admin list is the
        // screen a РОП leaves open, and N+1 on a list of runs is the shape that makes it feel broken.
        var counts = await databaseContext.ContentAdaptationItems
            .AsNoTracking()
            .Where(item => jobIds.Contains(item.JobId))
            .GroupBy(item => new { item.JobId, item.Status })
            .Select(group => new { group.Key.JobId, group.Key.Status, Count = group.Count() })
            .ToListAsync(cancellationToken);

        return jobs
            .Select(job => ToSummary(
                job,
                counts.Where(count => count.JobId == job.Id)
                    .ToDictionary(count => count.Status, count => count.Count, StringComparer.Ordinal)))
            .ToList();
    }

    public async Task<ContentAdaptationJobDto?> GetJobAsync(
        Guid jobId,
        CancellationToken cancellationToken = default)
    {
        await using var tenantScope = await TenantTransactionScope.BeginReadAsync(databaseContext, cancellationToken);

        var job = await databaseContext.ContentAdaptationJobs
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == jobId, cancellationToken);
        if (job is null)
        {
            return null;
        }

        var items = await ReadItemsAsync(jobId, cancellationToken);

        return new ContentAdaptationJobDto(
            ToSummary(job, CountByStatus(items)),
            items.Select(ToItemSummary).ToList());
    }

    public async Task<ContentAdaptationItemDto?> GetItemAsync(
        Guid jobId,
        Guid itemId,
        CancellationToken cancellationToken = default)
    {
        await using var tenantScope = await TenantTransactionScope.BeginReadAsync(databaseContext, cancellationToken);

        var item = await databaseContext.ContentAdaptationItems
            .AsNoTracking()
            .FirstOrDefaultAsync(
                candidate => candidate.Id == itemId && candidate.JobId == jobId, cancellationToken);

        return item is null ? null : await DescribeItemAsync(item, cancellationToken);
    }

    public async Task<ContentAdaptationJobDto> StartAsync(
        StartContentAdaptationRequestDto request,
        Guid? actorId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var organizationId = tenantContext.OrganizationId
            ?? throw new ContentAdaptationValidationException(
                "A content adaptation batch belongs to exactly one organization.");

        var mode = string.IsNullOrWhiteSpace(request.Mode)
            ? ContentAdaptationModes.ToneRewrite
            : request.Mode.Trim();
        if (!ContentAdaptationModes.IsKnown(mode))
        {
            throw new ContentAdaptationValidationException(
                $"Unknown mode '{mode}'. Valid values: {string.Join(", ", ContentAdaptationModes.All)}.");
        }

        var stageKey = (request.StageKey ?? string.Empty).Trim();
        if (stageKey.Length == 0 || stageKey.Length > MaximumStageKeyLength)
        {
            throw new ContentAdaptationValidationException(
                $"stageKey is required and must be at most {MaximumStageKeyLength} characters.");
        }

        await using var tenantScope = await TenantTransactionScope.BeginWriteAsync(databaseContext, cancellationToken);

        // Checked here for the message, enforced by UX_ContentAdaptationJobs_Live for the truth: two
        // clicks a second apart would both read no live batch under READ COMMITTED, and the customer
        // would pay twice for the same sixty rewrites. The read exists so the second click gets a
        // sentence instead of a unique-violation.
        var liveJob = await databaseContext.ContentAdaptationJobs
            .AsNoTracking()
            .FirstOrDefaultAsync(
                candidate => candidate.Mode == mode
                             && candidate.StageKey == stageKey
                             && ContentAdaptationStatuses.Live.Contains(candidate.Status),
                cancellationToken);

        if (liveJob is not null)
        {
            throw new ContentAdaptationStateException(
                $"A live '{mode}' batch for stage '{stageKey}' already exists (JobId={liveJob.Id}). "
                + "Finish reviewing it before starting another.");
        }

        var maximumItems = Math.Clamp(options.Value.MaximumItemsPerJob, 1, 500);
        var scopeSize = await ContentAdaptationScopeCollector.CountAsync(
            databaseContext, stageKey, cancellationToken);

        if (scopeSize == 0)
        {
            throw new ContentAdaptationValidationException(
                $"Stage '{stageKey}' has no exercises to adapt.");
        }

        // Refused with the number rather than truncated to the ceiling. «Все упражнения этапа» is a
        // promise, and silently adapting the first sixty of four hundred is the kind of half-kept
        // promise nobody discovers until a manager asks why half the stage still sounds generic.
        if (scopeSize > maximumItems)
        {
            throw new ContentAdaptationValidationException(
                $"Stage '{stageKey}' holds {scopeSize} exercises, which is above the per-batch ceiling "
                + $"of {maximumItems}. Split the stage across smaller skills, or raise "
                + "ContentAdaptation:MaximumItemsPerJob knowing that each exercise costs one AI call.");
        }

        var scope = await ContentAdaptationScopeCollector.CollectAsync(
            databaseContext, stageKey, maximumItems, cancellationToken);

        var now = DateTime.UtcNow;
        var job = new ContentAdaptationJob
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            CreatedBy = actorId,
            Mode = mode,
            StageKey = stageKey,
            Status = ContentAdaptationStatuses.Preparing,
            ItemCount = scope.Count,
            CreatedAt = now,
            UpdatedAt = now
        };

        databaseContext.ContentAdaptationJobs.Add(job);

        foreach (var row in scope)
        {
            databaseContext.ContentAdaptationItems.Add(new ContentAdaptationItem
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                JobId = job.Id,
                ExerciseId = row.ExerciseId,
                LessonId = row.LessonId,
                LessonTitle = Truncate(row.LessonTitle, MaximumLessonTitleLength),
                ExerciseType = row.ExerciseType,
                OrderInLesson = row.OrderInLesson,
                // The fingerprint of the body the model is about to be shown, recorded before the
                // call rather than after it: what accept has to compare against is what was proposed
                // from, and a hash taken later would silently absorb an edit made in between.
                BaseContentHash = HashExercise(row.ExerciseType, row.SerializedContent, row.CustomAiPrompt),
                Status = ContentAdaptationItemStatuses.Pending,
                CreatedAt = now,
                UpdatedAt = now
            });
        }

        await databaseContext.SaveChangesAsync(cancellationToken);

        // Read back before the commit, not after. Outside a transaction there is no
        // SET LOCAL app.organization_id, so the row-level-security policy returns nothing and the
        // response would describe an empty batch that was in fact just written.
        var items = await ReadItemsAsync(job.Id, cancellationToken);

        await tenantScope.CommitAsync(cancellationToken);

        logger.LogInformation(
            "Content adaptation batch created JobId={JobId} Mode={Mode} Stage={StageKey} Items={ItemCount}",
            job.Id, mode, stageKey, scope.Count);

        return new ContentAdaptationJobDto(
            ToSummary(job, CountByStatus(items)),
            items.Select(ToItemSummary).ToList());
    }

    public async Task<ContentAdaptationItemDto?> AcceptItemAsync(
        Guid jobId,
        Guid itemId,
        Guid? actorId,
        CancellationToken cancellationToken = default)
    {
        await using var tenantScope = await TenantTransactionScope.BeginWriteAsync(databaseContext, cancellationToken);

        var item = await databaseContext.ContentAdaptationItems
            .FirstOrDefaultAsync(candidate => candidate.Id == itemId && candidate.JobId == jobId, cancellationToken);
        if (item is null)
        {
            return null;
        }

        var job = await databaseContext.ContentAdaptationJobs
            .FirstOrDefaultAsync(candidate => candidate.Id == jobId, cancellationToken);
        if (job is null)
        {
            return null;
        }

        // A finding is a diagnosis, not a patch. The review half deliberately produces nothing that
        // can be applied, so that a model can never be the thing that edited a customer's curriculum.
        if (job.Mode != ContentAdaptationModes.ToneRewrite)
        {
            throw new ContentAdaptationStateException(
                "A quality-review finding has nothing to apply. Fix the exercise in the editor, or run "
                + "a tone rewrite over the stage.");
        }

        if (item.Status != ContentAdaptationItemStatuses.Proposed)
        {
            throw new ContentAdaptationStateException(
                $"Item {itemId} is '{item.Status}' and only a proposed item can be accepted.");
        }

        if (string.IsNullOrWhiteSpace(item.ProposedContent))
        {
            throw new ContentAdaptationStateException($"Item {itemId} carries no proposal to apply.");
        }

        var proposed = ParseOrThrow(item.ProposedContent, itemId);

        // Re-validated on the way out as well as on the way in. The proposal may have been written by
        // an older build, and an exercise the renderers cannot play is worse than one that reads
        // generically — the second is disappointing, the first is a blank screen mid-lesson.
        var validationErrors = ExerciseContentValidator.Validate(item.ExerciseType, proposed.RootElement);
        if (validationErrors.Count > 0)
        {
            throw new ContentAdaptationStateException(
                $"The proposal for item {itemId} no longer passes validation: {string.Join(" ", validationErrors)}");
        }

        var target = await ResolveTargetExerciseAsync(item, actorId, cancellationToken);

        // The staleness check, and it is deliberately made against the row about to be written rather
        // than against the row the proposal was read from. Those are the same row for a lesson the
        // organization owns, and they are the base and its fresh copy for one it does not — a copy
        // that is byte-identical, which is exactly why the same comparison covers both. When they
        // differ, somebody edited the exercise after the model saw it, and applying would discard
        // their edit: 40.18 refused to build a three-way merge for precisely this, and refusing is
        // the same answer.
        var currentHash = HashExercise(target.Type, target.SerializedContent, target.CustomAiPrompt);
        if (!string.Equals(currentHash, item.BaseContentHash, StringComparison.Ordinal))
        {
            throw new ContentAdaptationStateException(
                $"Exercise {target.Id} has changed since this proposal was computed. Re-run the batch "
                + "rather than overwriting the newer wording.");
        }

        var now = DateTime.UtcNow;
        target.SerializedContent = proposed.RootElement.GetRawText();
        target.UpdatedAt = now;

        item.Status = ContentAdaptationItemStatuses.Accepted;
        item.AppliedExerciseId = target.Id;
        item.AppliedAt = now;
        item.ResolvedBy = actorId;
        item.ResolvedAt = now;
        item.UpdatedAt = now;

        await RefreshJobStatusAsync(job, now, cancellationToken);

        await databaseContext.SaveChangesAsync(cancellationToken);

        // Described before the commit, for the reason StartAsync gives: outside the transaction the
        // tenant session variable is gone and every read comes back empty.
        var described = await DescribeItemAsync(item, cancellationToken);

        await tenantScope.CommitAsync(cancellationToken);

        logger.LogInformation(
            "Content adaptation proposal applied JobId={JobId} ItemId={ItemId} ExerciseId={ExerciseId} "
            + "AppliedExerciseId={AppliedExerciseId} ActorId={ActorId}",
            jobId, itemId, item.ExerciseId, target.Id, actorId);

        return described;
    }

    public async Task<ContentAdaptationItemDto?> RejectItemAsync(
        Guid jobId,
        Guid itemId,
        Guid? actorId,
        CancellationToken cancellationToken = default)
    {
        await using var tenantScope = await TenantTransactionScope.BeginWriteAsync(databaseContext, cancellationToken);

        var item = await databaseContext.ContentAdaptationItems
            .FirstOrDefaultAsync(candidate => candidate.Id == itemId && candidate.JobId == jobId, cancellationToken);
        if (item is null)
        {
            return null;
        }

        var job = await databaseContext.ContentAdaptationJobs
            .FirstOrDefaultAsync(candidate => candidate.Id == jobId, cancellationToken);
        if (job is null)
        {
            return null;
        }

        if (item.Status != ContentAdaptationItemStatuses.Proposed)
        {
            throw new ContentAdaptationStateException(
                $"Item {itemId} is '{item.Status}' and only a proposed item can be rejected.");
        }

        var now = DateTime.UtcNow;
        item.Status = ContentAdaptationItemStatuses.Rejected;
        item.ResolvedBy = actorId;
        item.ResolvedAt = now;
        item.UpdatedAt = now;

        // Nothing is written to content and nothing is remembered about the refusal beyond the row
        // itself. A rejected rewrite is not a standing instruction — the next batch is a new question
        // asked of a possibly different profile, and suppressing it here would quietly turn one «нет»
        // into a permanent exemption nobody can find later.
        await RefreshJobStatusAsync(job, now, cancellationToken);

        await databaseContext.SaveChangesAsync(cancellationToken);

        var described = await DescribeItemAsync(item, cancellationToken);

        await tenantScope.CommitAsync(cancellationToken);

        return described;
    }

    public async Task<ContentAdaptationJobDto?> RetryAsync(
        Guid jobId,
        CancellationToken cancellationToken = default)
    {
        await using var tenantScope = await TenantTransactionScope.BeginWriteAsync(databaseContext, cancellationToken);

        var job = await databaseContext.ContentAdaptationJobs
            .FirstOrDefaultAsync(candidate => candidate.Id == jobId, cancellationToken);
        if (job is null)
        {
            return null;
        }

        var items = await databaseContext.ContentAdaptationItems
            .Where(item => item.JobId == jobId)
            .ToListAsync(cancellationToken);

        var failedItems = items
            .Where(item => item.Status == ContentAdaptationItemStatuses.Failed)
            .ToList();

        if (failedItems.Count == 0)
        {
            throw new ContentAdaptationStateException($"Batch {jobId} has no failed items to retry.");
        }

        var now = DateTime.UtcNow;
        foreach (var item in failedItems)
        {
            item.Status = ContentAdaptationItemStatuses.Pending;
            item.Attempts = 0;
            item.FailureReason = null;
            item.UpdatedAt = now;
        }

        job.FailureReason = null;
        job.ClaimedAt = null;
        ContentAdaptationStatusCalculator.Apply(job, items, now);
        job.UpdatedAt = now;

        await databaseContext.SaveChangesAsync(cancellationToken);
        await tenantScope.CommitAsync(cancellationToken);

        logger.LogInformation(
            "Content adaptation batch re-queued JobId={JobId} RetriedItems={RetriedItemCount}",
            jobId, failedItems.Count);

        return await GetJobAsync(jobId, cancellationToken);
    }

    /// <summary>
    /// Where an accepted rewrite is actually written.
    ///
    /// <para>
    /// <b>A global-library exercise is never edited in place</b> — that row is every other customer's
    /// curriculum, and RLS cannot tell "global" from "somebody else's" because global is a null
    /// (<c>ContentAuthoringGuard</c>). So the lesson is forked first, exactly as pressing "edit"
    /// would fork it, and the body lands in the organization's own copy. This is the copy-on-write
    /// moment 40.18 built and the reason this block did not need a content-writing path of its own.
    /// </para>
    ///
    /// <para>
    /// <b>The copy is addressed positionally</b>, because the fork clones exercises in
    /// <c>(OrderInLesson, Id)</c> order with fresh ids and keeps no mapping. Positional addressing is
    /// exact for a copy nobody has touched, and when it is not — different counts, a different type
    /// at that index — the answer is a refusal rather than a guess, because guessing wrong here means
    /// writing one exercise's rewrite over a different exercise.
    /// </para>
    /// </summary>
    private async Task<Exercise> ResolveTargetExerciseAsync(
        ContentAdaptationItem item,
        Guid? actorId,
        CancellationToken cancellationToken)
    {
        var sourceExercise = await databaseContext.Exercises
            .FirstOrDefaultAsync(candidate => candidate.Id == item.ExerciseId, cancellationToken)
            ?? throw new ContentAdaptationStateException(
                $"Exercise {item.ExerciseId} no longer exists; nothing to apply the proposal to.");

        if (sourceExercise.OrganizationId is not null)
        {
            return sourceExercise;
        }

        var overrideResult = await contentOverrideService.CreateOverrideAsync(
            ContentOverrideKinds.Lesson, item.LessonId, actorId, cancellationToken);

        if (overrideResult.Override is null)
        {
            throw new ContentAdaptationStateException(
                $"Could not fork lesson {item.LessonId} for this organization "
                + $"({overrideResult.Outcome}); the proposal was not applied.");
        }

        var overrideLessonId = overrideResult.Override.OverrideId;

        var baseExercises = await ReadOrderedExercisesAsync(item.LessonId, cancellationToken);
        var overrideExercises = await ReadOrderedExercisesAsync(overrideLessonId, cancellationToken);

        var index = baseExercises.FindIndex(candidate => candidate.Id == item.ExerciseId);
        if (index < 0 || baseExercises.Count != overrideExercises.Count)
        {
            throw new ContentAdaptationStateException(
                $"Your copy of lesson {item.LessonId} no longer has the same exercises as the base "
                + "lesson this proposal was computed from. Apply the change by hand, or re-run the batch.");
        }

        var target = overrideExercises[index];
        if (!string.Equals(target.Type, item.ExerciseType, StringComparison.Ordinal))
        {
            throw new ContentAdaptationStateException(
                $"Your copy of lesson {item.LessonId} has a different exercise at position "
                + $"{item.OrderInLesson}. Apply the change by hand, or re-run the batch.");
        }

        return target;
    }

    private Task<List<Exercise>> ReadOrderedExercisesAsync(Guid lessonId, CancellationToken cancellationToken)
        => databaseContext.Exercises
            .Where(exercise => exercise.LessonId == lessonId)
            .OrderBy(exercise => exercise.OrderInLesson)
            .ThenBy(exercise => exercise.Id)
            .ToListAsync(cancellationToken);

    private async Task RefreshJobStatusAsync(
        ContentAdaptationJob job,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var items = await databaseContext.ContentAdaptationItems
            .Where(item => item.JobId == job.Id)
            .ToListAsync(cancellationToken);

        ContentAdaptationStatusCalculator.Apply(job, items, now);
    }

    private async Task<List<ContentAdaptationItem>> ReadItemsAsync(
        Guid jobId,
        CancellationToken cancellationToken)
        => await databaseContext.ContentAdaptationItems
            .AsNoTracking()
            .Where(item => item.JobId == jobId)
            .OrderBy(item => item.LessonTitle)
            .ThenBy(item => item.OrderInLesson)
            .ThenBy(item => item.Id)
            .ToListAsync(cancellationToken);

    private async Task<ContentAdaptationItemDto> DescribeItemAsync(
        ContentAdaptationItem item,
        CancellationToken cancellationToken)
    {
        var currentExercise = await databaseContext.Exercises
            .AsNoTracking()
            .FirstOrDefaultAsync(
                candidate => candidate.Id == (item.AppliedExerciseId ?? item.ExerciseId), cancellationToken);

        var currentContent = TryParseElement(currentExercise?.SerializedContent);
        var proposedContent = TryParseElement(item.ProposedContent);

        var changes = ContentFieldChangeSummarizer.Compare(
            currentExercise?.SerializedContent, item.ProposedContent);

        // Staleness is computed here and stored nowhere, the shape 40.18 chose: there is no flag to
        // set, so there is no flag to be wrong, and an item stops being stale the moment the exercise
        // is put back the way it was.
        var isStale = currentExercise is not null
                      && item.Status == ContentAdaptationItemStatuses.Proposed
                      && !string.Equals(
                          HashExercise(
                              currentExercise.Type,
                              currentExercise.SerializedContent,
                              currentExercise.CustomAiPrompt),
                          item.BaseContentHash,
                          StringComparison.Ordinal);

        return new ContentAdaptationItemDto(
            ToItemSummary(item),
            currentContent,
            proposedContent,
            changes,
            ContentReviewFindingDocumentSerializer.Describe(item.Findings),
            isStale);
    }

    private static ContentAdaptationJobSummaryDto ToSummary(
        ContentAdaptationJob job,
        IReadOnlyDictionary<string, int> countsByStatus)
        => new(
            job.Id,
            job.Mode,
            job.StageKey,
            job.Status,
            job.ItemCount,
            CountOf(countsByStatus, ContentAdaptationItemStatuses.Pending),
            CountOf(countsByStatus, ContentAdaptationItemStatuses.Proposed),
            CountOf(countsByStatus, ContentAdaptationItemStatuses.Accepted),
            CountOf(countsByStatus, ContentAdaptationItemStatuses.Rejected),
            CountOf(countsByStatus, ContentAdaptationItemStatuses.Unchanged),
            CountOf(countsByStatus, ContentAdaptationItemStatuses.Failed),
            job.FailureReason,
            job.CreatedAt,
            job.UpdatedAt,
            job.CompletedAt);

    private static ContentAdaptationItemSummaryDto ToItemSummary(ContentAdaptationItem item)
        => new(
            item.Id,
            item.ExerciseId,
            item.LessonId,
            item.LessonTitle,
            item.ExerciseType,
            item.OrderInLesson,
            item.Status,
            item.ChangeSummary,
            ContentReviewFindingDocumentSerializer.Deserialize(item.Findings).Count,
            ContentReviewFindingDocumentSerializer.HasBlockingFinding(item.Findings),
            item.ChangedFieldCount,
            item.FailureReason,
            item.ResolvedAt);

    private static IReadOnlyDictionary<string, int> CountByStatus(IReadOnlyCollection<ContentAdaptationItem> items)
        => items
            .GroupBy(item => item.Status, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);

    private static int CountOf(IReadOnlyDictionary<string, int> counts, string status)
        => counts.TryGetValue(status, out var count) ? count : 0;

    private static string HashExercise(string type, string? serializedContent, string? customAiPrompt)
        => ContentSnapshotSerializer.ComputeContentHash(
            ContentSnapshotSerializer.BuildCanonicalExerciseBody(type, serializedContent, customAiPrompt));

    private static JsonDocument ParseOrThrow(string json, Guid itemId)
    {
        try
        {
            return JsonDocument.Parse(json);
        }
        catch (JsonException exception)
        {
            throw new ContentAdaptationStateException(
                $"The stored proposal for item {itemId} is not readable JSON: {exception.Message}");
        }
    }

    /// <summary>
    /// Reads a stored document for display. Unparseable text yields <see langword="null"/> rather
    /// than an exception — the same tolerance every other reader of a stored document in this service
    /// shows, because a queue that refuses to render is worse than one item that renders empty.
    /// </summary>
    private static JsonElement? TryParseElement(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<JsonElement>(json, SerializerOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string Truncate(string value, int maximumLength)
        => value.Length <= maximumLength ? value : value[..maximumLength];
}
