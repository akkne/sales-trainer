using Microsoft.EntityFrameworkCore;
using Sellevate.Ai.Features.Dialog.Models;
using Sellevate.Ai.Infrastructure.Data;

namespace Sellevate.Ai.Features.Dialog.Overrides;

public interface IDialogModeOverrideService
{
    Task<DialogModeOverrideResult> CreateOverrideAsync(Guid baseModeId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DialogModeOverrideDto>> GetOverridesAsync(
        bool staleOnly, CancellationToken cancellationToken = default);

    Task<DialogModeOverrideReviewDto?> GetReviewAsync(Guid overrideId, CancellationToken cancellationToken = default);

    Task<bool> AcceptBaseAsync(Guid overrideId, CancellationToken cancellationToken = default);

    Task<bool> KeepOverrideAsync(Guid overrideId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Phase 40.18. Copy-on-write for dialog prompts (docs/TENANCY/CONTENT_MODEL.md §2.6, §4).
///
/// <para>
/// <b>No cross-service event, deliberately.</b> The obvious design puts a Kafka message between
/// learning-service and ai-service so that publishing content in one marks overrides stale in the
/// other. It is not needed and would be a liability: an override and the base it forked from are
/// always the same content family in the same database, so staleness is an intra-database join
/// everywhere it is asked. A message would add a delivery guarantee, an ordering question and a
/// dead-letter path to a query that cannot be wrong (docs/DECISIONS.md, 2026-08-18).
/// </para>
///
/// <para>
/// <b>Retiring an override is <c>IsActive = false</c>, not a delete and not a new column.</b>
/// Mongo dialog sessions carry <c>ModeId</c> without a foreign key, so deleting the row to tidy a
/// review queue would orphan every recorded conversation that used it. The mode list already
/// filters on <c>IsActive</c>, and so does resolution, so an inactive override stops shadowing its
/// base and the global prompt comes back — which is exactly what "take the new base" means.
/// </para>
/// </summary>
internal sealed class DialogModeOverrideService(
    AiDbContext databaseContext,
    ILogger<DialogModeOverrideService> logger) : IDialogModeOverrideService
{
    public async Task<DialogModeOverrideResult> CreateOverrideAsync(
        Guid baseModeId,
        CancellationToken cancellationToken = default)
    {
        if (databaseContext.TenantContext.OrganizationId is not { } organizationId)
        {
            return new DialogModeOverrideResult(DialogModeOverrideOutcome.NoOrganization, null);
        }

        await using var tenantScope = await AiTenantTransactionScope.BeginWriteAsync(databaseContext, cancellationToken);

        var baseMode = await databaseContext.DialogModes
            .AsNoTracking()
            .Include(mode => mode.Bundle)
            .FirstOrDefaultAsync(mode => mode.Id == baseModeId, cancellationToken);

        if (baseMode is null)
        {
            return new DialogModeOverrideResult(DialogModeOverrideOutcome.SourceNotFound, null);
        }

        if (baseMode.OrganizationId is not null)
        {
            return new DialogModeOverrideResult(DialogModeOverrideOutcome.SourceNotGlobal, null);
        }

        // The seeded hidden bundles stay global. Their prompts are half code: the service fills in
        // placeholders (the company being called, the scenario the learner typed), and a copy would
        // drift away from the code that feeds it until it silently stopped matching.
        if (baseMode.Bundle is { IsHidden: true })
        {
            return new DialogModeOverrideResult(DialogModeOverrideOutcome.SourceIsSeededHiddenMode, null);
        }

        var existing = await databaseContext.DialogModes
            .FirstOrDefaultAsync(
                mode => mode.ParentModeId == baseModeId && mode.OrganizationId == organizationId,
                cancellationToken);

        if (existing is { IsActive: true })
        {
            return new DialogModeOverrideResult(
                DialogModeOverrideOutcome.AlreadyExists,
                await DescribeAsync(existing, cancellationToken));
        }

        // A retired override is revived rather than duplicated: UNIQUE (OrganizationId, BundleId,
        // Key) makes a second copy impossible, and the organization had already discarded its text
        // when it accepted the base, so pressing "edit" again is a fresh fork.
        var overrideMode = existing ?? new DialogMode { Id = Guid.NewGuid(), CreatedAt = DateTime.UtcNow };

        overrideMode.OrganizationId = organizationId;
        overrideMode.ParentModeId = baseMode.Id;
        overrideMode.BundleId = baseMode.BundleId;
        overrideMode.Key = baseMode.Key;
        overrideMode.Title = baseMode.Title;
        overrideMode.Description = baseMode.Description;
        overrideMode.ChatSystemPrompt = baseMode.ChatSystemPrompt;
        overrideMode.FeedbackSystemPrompt = baseMode.FeedbackSystemPrompt;
        overrideMode.SortOrder = baseMode.SortOrder;
        overrideMode.IsActive = true;
        overrideMode.VoiceEnabled = baseMode.VoiceEnabled;
        overrideMode.VoiceId = baseMode.VoiceId;
        overrideMode.UpdatedAt = DateTime.UtcNow;
        overrideMode.BaseContentHash = DialogModeSnapshot.ComputeContentHash(baseMode);

        if (existing is null)
        {
            databaseContext.DialogModes.Add(overrideMode);
        }

        await databaseContext.SaveChangesAsync(cancellationToken);
        await tenantScope.CommitAsync(cancellationToken);

        logger.LogInformation(
            "Dialog mode override created BaseModeId={BaseModeId} OverrideId={OverrideId} Key={Key} OrganizationId={OrganizationId}",
            baseModeId, overrideMode.Id, overrideMode.Key, organizationId);

        return new DialogModeOverrideResult(
            DialogModeOverrideOutcome.Created,
            await DescribeAsync(overrideMode, cancellationToken));
    }

    public async Task<IReadOnlyList<DialogModeOverrideDto>> GetOverridesAsync(
        bool staleOnly,
        CancellationToken cancellationToken = default)
    {
        if (databaseContext.TenantContext.OrganizationId is not { } organizationId)
        {
            return [];
        }

        await using var tenantScope = await AiTenantTransactionScope.BeginReadAsync(databaseContext, cancellationToken);

        var overrides = await databaseContext.DialogModes
            .AsNoTracking()
            .Where(mode => mode.OrganizationId == organizationId && mode.ParentModeId != null && mode.IsActive)
            .OrderBy(mode => mode.SortOrder)
            .ThenBy(mode => mode.Key)
            .ToListAsync(cancellationToken);

        var described = new List<DialogModeOverrideDto>(overrides.Count);
        foreach (var overrideMode in overrides)
        {
            described.Add(await DescribeAsync(overrideMode, cancellationToken));
        }

        return staleOnly ? described.Where(entry => entry.IsStale).ToList() : described;
    }

    public async Task<DialogModeOverrideReviewDto?> GetReviewAsync(
        Guid overrideId,
        CancellationToken cancellationToken = default)
    {
        if (databaseContext.TenantContext.OrganizationId is not { } organizationId)
        {
            return null;
        }

        await using var tenantScope = await AiTenantTransactionScope.BeginReadAsync(databaseContext, cancellationToken);

        var overrideMode = await FindOwnOverrideAsync(overrideId, organizationId, cancellationToken);
        if (overrideMode is null)
        {
            return null;
        }

        var baseMode = await ReadBaseAsync(overrideMode.ParentModeId!.Value, cancellationToken);

        return new DialogModeOverrideReviewDto(
            await DescribeAsync(overrideMode, cancellationToken),
            overrideMode.ChatSystemPrompt,
            overrideMode.FeedbackSystemPrompt,
            baseMode?.ChatSystemPrompt,
            baseMode?.FeedbackSystemPrompt);
    }

    public async Task<bool> AcceptBaseAsync(Guid overrideId, CancellationToken cancellationToken = default)
    {
        if (databaseContext.TenantContext.OrganizationId is not { } organizationId)
        {
            return false;
        }

        await using var tenantScope = await AiTenantTransactionScope.BeginWriteAsync(databaseContext, cancellationToken);

        var overrideMode = await FindOwnOverrideAsync(overrideId, organizationId, cancellationToken);
        if (overrideMode is null)
        {
            return false;
        }

        overrideMode.IsActive = false;
        overrideMode.UpdatedAt = DateTime.UtcNow;

        await databaseContext.SaveChangesAsync(cancellationToken);
        await tenantScope.CommitAsync(cancellationToken);

        logger.LogInformation(
            "Dialog mode override retired in favour of the base OverrideId={OverrideId} OrganizationId={OrganizationId}",
            overrideId, organizationId);

        return true;
    }

    public async Task<bool> KeepOverrideAsync(Guid overrideId, CancellationToken cancellationToken = default)
    {
        if (databaseContext.TenantContext.OrganizationId is not { } organizationId)
        {
            return false;
        }

        await using var tenantScope = await AiTenantTransactionScope.BeginWriteAsync(databaseContext, cancellationToken);

        var overrideMode = await FindOwnOverrideAsync(overrideId, organizationId, cancellationToken);
        if (overrideMode is null)
        {
            return false;
        }

        var baseMode = await ReadBaseAsync(overrideMode.ParentModeId!.Value, cancellationToken);
        if (baseMode is null)
        {
            return false;
        }

        // The prompt itself is untouched. This records "we looked at what changed upstream and ours
        // still stands", which is a decision, not a merge.
        overrideMode.BaseContentHash = DialogModeSnapshot.ComputeContentHash(baseMode);

        await databaseContext.SaveChangesAsync(cancellationToken);
        await tenantScope.CommitAsync(cancellationToken);

        logger.LogInformation(
            "Dialog mode override kept and re-pointed at the current base OverrideId={OverrideId} OrganizationId={OrganizationId}",
            overrideId, organizationId);

        return true;
    }

    private async Task<DialogModeOverrideDto> DescribeAsync(DialogMode overrideMode, CancellationToken cancellationToken)
    {
        var baseMode = await ReadBaseAsync(overrideMode.ParentModeId!.Value, cancellationToken);
        var currentBaseHash = baseMode is null ? null : DialogModeSnapshot.ComputeContentHash(baseMode);

        return new DialogModeOverrideDto(
            overrideMode.Id,
            overrideMode.ParentModeId.Value,
            overrideMode.BundleId,
            overrideMode.Key,
            overrideMode.Title,
            currentBaseHash is not null && currentBaseHash != overrideMode.BaseContentHash,
            overrideMode.BaseContentHash,
            currentBaseHash);
    }

    private Task<DialogMode?> FindOwnOverrideAsync(
        Guid overrideId,
        Guid organizationId,
        CancellationToken cancellationToken)
        => databaseContext.DialogModes.FirstOrDefaultAsync(
            mode => mode.Id == overrideId
                    && mode.OrganizationId == organizationId
                    && mode.ParentModeId != null,
            cancellationToken);

    private Task<DialogMode?> ReadBaseAsync(Guid baseModeId, CancellationToken cancellationToken)
        => databaseContext.DialogModes
            .AsNoTracking()
            .FirstOrDefaultAsync(mode => mode.Id == baseModeId, cancellationToken);
}
