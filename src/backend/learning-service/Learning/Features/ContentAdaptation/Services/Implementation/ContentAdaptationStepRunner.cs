using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Sellevate.Learning.Common.Constants;
using Sellevate.Learning.Features.Content;
using Sellevate.Learning.Features.Content.Services.Abstract;
using Sellevate.Learning.Features.ContentAdaptation.Models;
using Sellevate.Learning.Features.ContentAdaptation.Services.Abstract;
using Sellevate.Learning.Features.ContentGeneration.Models;
using Sellevate.Learning.Features.Exercises.Services;
using Sellevate.Learning.Infrastructure.Ai;
using Sellevate.Learning.Infrastructure.Data;

namespace Sellevate.Learning.Features.ContentAdaptation.Services.Implementation;

/// <summary>
/// Phase 40.32. One tick of the batch worker: claim a batch, answer a few of its items, write what
/// came back.
///
/// <para>
/// <b>The claim is on the batch and the idempotency is on the item.</b> 40.27 could put both on the
/// run because a run makes two calls; a batch makes sixty, and a lease that only protected the batch
/// would let a crash at item forty re-pay for the thirty-nine before it. So the lease says "somebody
/// is working on this batch right now" and the item's own state says "this exercise has already been
/// paid for" — an item that carries an answer is never <c>pending</c> again, whatever happens to the
/// process holding the lease. That is what makes an interrupted batch cost exactly one call.
/// </para>
///
/// <para>
/// <b>Each item is committed on its own.</b> Sixty calls cannot be held inside a transaction, and
/// batching the writes would mean a tick that dies at item four discards three answers it has
/// already been billed for. So the loop is call → commit → call, and the cost of a crash is bounded
/// by one call rather than by one tick.
/// </para>
///
/// <para>
/// <b>Nothing here can reach an <c>Exercise</c>.</b> This class writes <c>ContentAdaptationItems</c>
/// and the batch's own status columns, and that is the whole list. Applying a proposal is
/// <c>ContentAdaptationJobService.AcceptItemAsync</c>, which only ever runs inside an administrator's
/// request — the roadmap's «никогда не автоприменение», stated as which types this file may write.
/// </para>
/// </summary>
internal sealed class ContentAdaptationStepRunner(
    LearningDbContext databaseContext,
    IAiContentPipelineClient aiContentPipelineClient,
    IOrganizationProfileProvider organizationProfileProvider,
    IOptions<ContentAdaptationOptions> options,
    ILogger<ContentAdaptationStepRunner> logger) : IContentAdaptationStepRunner
{
    private const int MaximumFailureReasonLength = 1000;
    private const int MaximumChangeSummaryLength = 500;

    public async Task<int> RunPendingAsync(CancellationToken cancellationToken = default)
    {
        var jobIds = await FindClaimableJobIdsAsync(cancellationToken);
        if (jobIds.Count == 0)
        {
            return 0;
        }

        var answeredCount = 0;

        foreach (var jobId in jobIds)
        {
            var claim = await TryClaimAsync(jobId, cancellationToken);
            if (claim is null)
            {
                continue;
            }

            try
            {
                answeredCount += await RunItemsAsync(claim, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Content adaptation tick failed for JobId={JobId}", jobId);
                await RecordBatchFailureAsync(jobId, exception.Message, CancellationToken.None);
            }
            finally
            {
                await ReleaseClaimAsync(jobId, CancellationToken.None);
            }
        }

        return answeredCount;
    }

    private async Task<IReadOnlyList<Guid>> FindClaimableJobIdsAsync(CancellationToken cancellationToken)
    {
        var leaseExpiry = DateTime.UtcNow.AddMinutes(-Math.Clamp(options.Value.ClaimLeaseMinutes, 1, 120));

        await using var tenantScope = await TenantTransactionScope.BeginReadAsync(databaseContext, cancellationToken);

        return await databaseContext.ContentAdaptationJobs
            .AsNoTracking()
            .Where(job => ContentAdaptationStatuses.WorkerOwned.Contains(job.Status)
                          && (job.ClaimedAt == null || job.ClaimedAt < leaseExpiry))
            .OrderBy(job => job.CreatedAt)
            .Take(Math.Clamp(options.Value.MaximumJobsPerTick, 1, 10))
            .Select(job => job.Id)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Stamps the lease in a transaction of its own that commits before any LLM call is made.
    ///
    /// <para>
    /// <b>A single conditional UPDATE, not read-then-write</b> — the same call 40.27 made and for the
    /// same reason: under READ COMMITTED a read-modify-save lets two instances both see a free lease,
    /// both stamp it, and both pay for the same sixty rewrites. The predicate travels inside the
    /// UPDATE, so exactly one tick reports a row.
    /// </para>
    /// </summary>
    private async Task<ClaimedContentAdaptationJob?> TryClaimAsync(Guid jobId, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var leaseExpiry = now.AddMinutes(-Math.Clamp(options.Value.ClaimLeaseMinutes, 1, 120));

        await using var tenantScope = await TenantTransactionScope.BeginWriteAsync(databaseContext, cancellationToken);

        var claimedRowCount = await databaseContext.ContentAdaptationJobs
            .Where(job => job.Id == jobId
                          && ContentAdaptationStatuses.WorkerOwned.Contains(job.Status)
                          && (job.ClaimedAt == null || job.ClaimedAt < leaseExpiry))
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(job => job.ClaimedAt, now)
                    .SetProperty(job => job.UpdatedAt, now),
                cancellationToken);

        if (claimedRowCount == 0)
        {
            return null;
        }

        var claimedJob = await databaseContext.ContentAdaptationJobs
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == jobId, cancellationToken);

        await tenantScope.CommitAsync(cancellationToken);

        return claimedJob is null ? null : new ClaimedContentAdaptationJob(claimedJob.Id, claimedJob.Mode);
    }

    private async Task<int> RunItemsAsync(
        ClaimedContentAdaptationJob claim,
        CancellationToken cancellationToken)
    {
        var maximumAttempts = Math.Clamp(options.Value.MaximumAttemptsPerItem, 1, 5);
        var itemIds = await ReadPendingItemIdsAsync(claim.JobId, maximumAttempts, cancellationToken);
        if (itemIds.Count == 0)
        {
            return 0;
        }

        // Read once per tick, not once per item: it is one row and it does not change between two
        // calls a few seconds apart. An empty profile is passed as nothing at all rather than as an
        // object of nulls — the same rule 40.27 follows.
        var profile = ContentStructureDto.FromProfile(
            await organizationProfileProvider.GetCurrentAsync(cancellationToken));

        var answeredCount = 0;

        foreach (var itemId in itemIds)
        {
            var work = await ReadItemWorkAsync(itemId, cancellationToken);
            if (work is null)
            {
                continue;
            }

            try
            {
                await AnswerItemAsync(claim, work, profile, cancellationToken);
                answeredCount++;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                // One exercise the model chokes on must not fail the other fifty-nine. The attempt is
                // already on the row, so a crash loop cannot re-buy the same call for ever.
                logger.LogWarning(
                    exception,
                    "Content adaptation item failed JobId={JobId} ItemId={ItemId}",
                    claim.JobId, itemId);

                await RecordItemFailureAsync(itemId, exception.Message, CancellationToken.None);
            }
        }

        await RefreshBatchStatusAsync(claim.JobId, CancellationToken.None);

        return answeredCount;
    }

    private async Task AnswerItemAsync(
        ClaimedContentAdaptationJob claim,
        ContentAdaptationItemWork work,
        ContentStructureDto profile,
        CancellationToken cancellationToken)
    {
        using var currentContent = JsonDocument.Parse(work.SerializedContent);

        var request = new AiAdaptExerciseRequest(
            work.ExerciseType,
            currentContent.RootElement,
            profile.IsEmpty ? null : profile);

        if (claim.Mode == ContentAdaptationModes.QualityReview)
        {
            var review = await aiContentPipelineClient.ReviewExerciseAsync(request, cancellationToken);
            await WriteReviewAsync(work, review, cancellationToken);
            return;
        }

        var rewrite = await aiContentPipelineClient.RewriteExerciseAsync(request, cancellationToken);
        await WriteRewriteAsync(work, currentContent.RootElement, rewrite, cancellationToken);
    }

    private async Task WriteRewriteAsync(
        ContentAdaptationItemWork work,
        JsonElement currentContent,
        AiRewrittenExercise rewrite,
        CancellationToken cancellationToken)
    {
        string? proposedJson = null;
        var changeCount = 0;

        if (rewrite.Content is { ValueKind: JsonValueKind.Object } proposedContent)
        {
            // Validated here rather than at accept time only. A proposal the renderers cannot play
            // must never reach the queue: a person cannot tell a broken body from a good one by
            // reading a diff, and an exercise that blanks the screen mid-lesson is a worse outcome
            // than one that still sounds generic.
            var validationErrors = ExerciseContentValidator.Validate(work.ExerciseType, proposedContent);
            if (validationErrors.Count > 0)
            {
                throw new InvalidOperationException(
                    $"The rewritten body failed validation: {string.Join(" ", validationErrors)}");
            }

            var changes = ContentFieldChangeSummarizer.Compare(currentContent, proposedContent);
            changeCount = changes.Count;

            // A rewrite that changed nothing is «без изменений», whatever the model said about it.
            // The comparison is over the parsed documents, so re-serialisation and key order cannot
            // manufacture a change that is not there.
            if (changeCount > 0)
            {
                proposedJson = proposedContent.GetRawText();
            }
        }

        await using var tenantScope = await TenantTransactionScope.BeginWriteAsync(databaseContext, cancellationToken);

        var item = await LoadPendingItemAsync(work.ItemId, cancellationToken);
        if (item is null)
        {
            return;
        }

        var now = DateTime.UtcNow;

        if (proposedJson is null)
        {
            item.Status = ContentAdaptationItemStatuses.Unchanged;
            item.ChangeSummary = null;
        }
        else
        {
            item.Status = ContentAdaptationItemStatuses.Proposed;
            item.ProposedContent = proposedJson;
            item.ChangeSummary = Truncate(rewrite.Summary, MaximumChangeSummaryLength);
            item.ChangedFieldCount = changeCount;
        }

        item.FailureReason = null;
        item.UpdatedAt = now;

        await databaseContext.SaveChangesAsync(cancellationToken);
        await tenantScope.CommitAsync(cancellationToken);
    }

    private async Task WriteReviewAsync(
        ContentAdaptationItemWork work,
        AiExerciseReview review,
        CancellationToken cancellationToken)
    {
        var findings = ContentReviewFindingDocumentSerializer.Normalize(
            (review.Findings ?? [])
                .Select(finding => new StoredContentReviewFinding(finding.Code, finding.Detail))
                .ToList());

        await using var tenantScope = await TenantTransactionScope.BeginWriteAsync(databaseContext, cancellationToken);

        var item = await LoadPendingItemAsync(work.ItemId, cancellationToken);
        if (item is null)
        {
            return;
        }

        var now = DateTime.UtcNow;

        // Finding nothing is the expected answer and resolves the item without ever reaching a
        // person's queue. A review that always produces at least one complaint is a review nobody
        // believes by the tenth exercise.
        if (findings.Count == 0)
        {
            item.Status = ContentAdaptationItemStatuses.Unchanged;
            item.Findings = null;
        }
        else
        {
            item.Status = ContentAdaptationItemStatuses.Proposed;
            item.Findings = ContentReviewFindingDocumentSerializer.Serialize(findings);
        }

        item.FailureReason = null;
        item.UpdatedAt = now;

        await databaseContext.SaveChangesAsync(cancellationToken);
        await tenantScope.CommitAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<Guid>> ReadPendingItemIdsAsync(
        Guid jobId,
        int maximumAttempts,
        CancellationToken cancellationToken)
    {
        await using var tenantScope = await TenantTransactionScope.BeginReadAsync(databaseContext, cancellationToken);

        return await databaseContext.ContentAdaptationItems
            .AsNoTracking()
            .Where(item => item.JobId == jobId
                           && item.Status == ContentAdaptationItemStatuses.Pending
                           && item.Attempts < maximumAttempts)
            .OrderBy(item => item.LessonId)
            .ThenBy(item => item.OrderInLesson)
            .ThenBy(item => item.Id)
            .Take(Math.Clamp(options.Value.MaximumItemsPerTick, 1, 20))
            .Select(item => item.Id)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Spends the item's attempt and reads the body to send, in one transaction that commits before
    /// the call. Returns null when another tick already took this item — the same conditional-UPDATE
    /// shape as the batch claim, one level down, because the money is spent per item.
    /// </summary>
    private async Task<ContentAdaptationItemWork?> ReadItemWorkAsync(
        Guid itemId,
        CancellationToken cancellationToken)
    {
        var maximumAttempts = Math.Clamp(options.Value.MaximumAttemptsPerItem, 1, 5);
        var now = DateTime.UtcNow;

        await using var tenantScope = await TenantTransactionScope.BeginWriteAsync(databaseContext, cancellationToken);

        var claimedRowCount = await databaseContext.ContentAdaptationItems
            .Where(item => item.Id == itemId
                           && item.Status == ContentAdaptationItemStatuses.Pending
                           && item.Attempts < maximumAttempts)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(item => item.Attempts, item => item.Attempts + 1)
                    .SetProperty(item => item.UpdatedAt, now),
                cancellationToken);

        if (claimedRowCount == 0)
        {
            return null;
        }

        // Two plain reads rather than one projection carrying a correlated subquery. The attempt is
        // already committed below either way, so a missing exercise costs an attempt and lands the
        // item in failed with a reason instead of spinning.
        var item = await databaseContext.ContentAdaptationItems
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == itemId, cancellationToken);

        var exercise = item is null
            ? null
            : await databaseContext.Exercises
                .AsNoTracking()
                .FirstOrDefaultAsync(candidate => candidate.Id == item.ExerciseId, cancellationToken);

        await tenantScope.CommitAsync(cancellationToken);

        if (item is null)
        {
            return null;
        }

        if (exercise is null)
        {
            // The exercise was deleted between collection and this tick. Nothing to propose about it
            // and nothing to fix.
            throw new InvalidOperationException(
                $"Exercise {item.ExerciseId} no longer exists; nothing to adapt.");
        }

        return new ContentAdaptationItemWork(
            item.Id,
            item.ExerciseType,
            exercise.SerializedContent,
            exercise.CustomAiPrompt);
    }

    private async Task<ContentAdaptationItem?> LoadPendingItemAsync(
        Guid itemId,
        CancellationToken cancellationToken)
    {
        var item = await databaseContext.ContentAdaptationItems
            .FirstOrDefaultAsync(candidate => candidate.Id == itemId, cancellationToken);

        // Somebody could have retried the batch or the row could have moved on while the call was in
        // flight. Writing a proposal over an item a person has already answered would resurrect a
        // decision they made.
        return item is { Status: ContentAdaptationItemStatuses.Pending } ? item : null;
    }

    private async Task RecordItemFailureAsync(Guid itemId, string reason, CancellationToken cancellationToken)
    {
        try
        {
            databaseContext.ChangeTracker.Clear();

            await using var tenantScope =
                await TenantTransactionScope.BeginWriteAsync(databaseContext, cancellationToken);

            var item = await databaseContext.ContentAdaptationItems
                .FirstOrDefaultAsync(candidate => candidate.Id == itemId, cancellationToken);
            if (item is null)
            {
                return;
            }

            item.FailureReason = Truncate(reason, MaximumFailureReasonLength);
            item.UpdatedAt = DateTime.UtcNow;

            if (item.Attempts >= Math.Clamp(options.Value.MaximumAttemptsPerItem, 1, 5))
            {
                item.Status = ContentAdaptationItemStatuses.Failed;
            }

            await databaseContext.SaveChangesAsync(cancellationToken);
            await tenantScope.CommitAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Could not record a content adaptation item failure ItemId={ItemId}", itemId);
        }
    }

    private async Task RecordBatchFailureAsync(Guid jobId, string reason, CancellationToken cancellationToken)
    {
        try
        {
            databaseContext.ChangeTracker.Clear();

            await using var tenantScope =
                await TenantTransactionScope.BeginWriteAsync(databaseContext, cancellationToken);

            var job = await databaseContext.ContentAdaptationJobs
                .FirstOrDefaultAsync(candidate => candidate.Id == jobId, cancellationToken);
            if (job is null)
            {
                return;
            }

            job.FailureReason = Truncate(reason, MaximumFailureReasonLength);
            job.UpdatedAt = DateTime.UtcNow;

            await databaseContext.SaveChangesAsync(cancellationToken);
            await tenantScope.CommitAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Could not record a content adaptation batch failure JobId={JobId}", jobId);
        }
    }

    /// <summary>
    /// Recomputes the batch's status from its items. Called at the end of every tick rather than
    /// after every item, because the status is a projection and re-deriving it once per tick is
    /// enough for a column nothing reads mid-tick.
    /// </summary>
    private async Task RefreshBatchStatusAsync(Guid jobId, CancellationToken cancellationToken)
    {
        try
        {
            databaseContext.ChangeTracker.Clear();

            await using var tenantScope =
                await TenantTransactionScope.BeginWriteAsync(databaseContext, cancellationToken);

            var job = await databaseContext.ContentAdaptationJobs
                .FirstOrDefaultAsync(candidate => candidate.Id == jobId, cancellationToken);
            if (job is null)
            {
                return;
            }

            var items = await databaseContext.ContentAdaptationItems
                .AsNoTracking()
                .Where(item => item.JobId == jobId)
                .ToListAsync(cancellationToken);

            if (ContentAdaptationStatusCalculator.Apply(job, items, DateTime.UtcNow))
            {
                await databaseContext.SaveChangesAsync(cancellationToken);
            }

            await tenantScope.CommitAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Could not refresh a content adaptation batch status JobId={JobId}", jobId);
        }
    }

    /// <summary>
    /// Releases the lease at the end of the tick so the next one can pick the batch up immediately
    /// rather than waiting out ten minutes. A tick that dies before reaching this leaves the lease
    /// standing, which is exactly what the lease is for.
    /// </summary>
    private async Task ReleaseClaimAsync(Guid jobId, CancellationToken cancellationToken)
    {
        try
        {
            databaseContext.ChangeTracker.Clear();

            await using var tenantScope =
                await TenantTransactionScope.BeginWriteAsync(databaseContext, cancellationToken);

            await databaseContext.ContentAdaptationJobs
                .Where(job => job.Id == jobId)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(job => job.ClaimedAt, (DateTime?)null),
                    cancellationToken);

            await tenantScope.CommitAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Could not release a content adaptation claim JobId={JobId}", jobId);
        }
    }

    private static string? Truncate(string? value, int maximumLength)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }

        return trimmed.Length <= maximumLength ? trimmed : trimmed[..maximumLength];
    }

    /// <summary>A claimed batch, detached from the tracker so the long calls hold no entity.</summary>
    private sealed record ClaimedContentAdaptationJob(Guid JobId, string Mode);

    /// <summary>One item's work order: what to send, without the entity it came from.</summary>
    private sealed record ContentAdaptationItemWork(
        Guid ItemId,
        string ExerciseType,
        string SerializedContent,
        string? CustomAiPrompt);
}
