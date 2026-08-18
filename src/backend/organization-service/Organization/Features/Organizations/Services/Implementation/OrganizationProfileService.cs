using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Sellevate.BuildingBlocks.Eventing;
using Sellevate.BuildingBlocks.Messaging;
using Sellevate.BuildingBlocks.Tenancy;
using Sellevate.Organization.Common.Constants;
using Sellevate.Organization.Eventing;
using Sellevate.Organization.Features.Organizations.Exceptions;
using Sellevate.Organization.Features.Organizations.Models;
using Sellevate.Organization.Features.Organizations.Services.Abstract;
using Sellevate.Organization.Infrastructure.Data;

namespace Sellevate.Organization.Features.Organizations.Services.Implementation;

/// <summary>
/// The only writer of the <c>OrganizationProfiles</c> row, and the only reader of it that other
/// services' replicas are built on. The organization is never a parameter here: it comes from the
/// scoped <c>ITenantContext</c>, so no caller can address a profile that is not its own
/// (docs/TENANCY/TENANCY.md §1.3).
///
/// <para>
/// <b>Every database access on this class opens an explicit transaction, including the reads.</b>
/// <c>SET LOCAL app.organization_id</c> — the statement <c>TenantConnectionInterceptor</c> issues to
/// drive row-level security — is scoped to a transaction, and EF opens an implicit one only around
/// <c>SaveChangesAsync</c>. A bare <c>SELECT</c> would therefore run with the setting unset under a
/// <c>NOBYPASSRLS</c> role and return nothing at all (docs/TENANCY/TENANCY.md §1.5).
/// </para>
/// </summary>
internal sealed class OrganizationProfileService(
    OrganizationDbContext databaseContext,
    ITenantContext tenantContext,
    IEventPublisher eventPublisher)
    : IOrganizationProfileService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public async Task<OrganizationProfileDto?> GetProfileAsync(CancellationToken cancellationToken = default)
    {
        var currentOrganizationId = RequireOrganizationId();

        await using var transaction = await databaseContext.Database.BeginTransactionAsync(cancellationToken);

        var profile = await databaseContext.OrganizationProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.OrganizationId == currentOrganizationId, cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return profile is null ? null : ToDto(profile);
    }

    public Task<OrganizationProfileDto> UpsertProfileAsync(
        UpdateOrganizationProfileRequestDto request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        return WriteProfileAsync(_ => request, cancellationToken);
    }

    /// <summary>
    /// Phase 40.29. Which questions the interview has left. A read, and a cheap one: nothing here
    /// calls a model, because which columns are blank is arithmetic
    /// (<see cref="OrganizationProfileGapInspector"/>).
    ///
    /// <para>
    /// A missing profile row is answered, not refused. It is the case the roadmap's second bullet is
    /// about — «профиль останется пустым» — and an organization that has never saved one has seven
    /// questions to answer, which is a more useful answer than «не найдено».
    /// </para>
    /// </summary>
    public async Task<OrganizationProfileGapsDto> GetGapsAsync(
        int questionLimit, CancellationToken cancellationToken = default)
    {
        var profile = await GetProfileAsync(cancellationToken);

        return OrganizationProfileGapInspector.Inspect(profile, questionLimit);
    }

    /// <summary>
    /// Phase 40.29. One answer to one question. The read and the write share a transaction, so an
    /// answer typed on one screen cannot lose an answer saved on another between them — the ordinary
    /// case here, since the interview exists to be answered by whoever knows that particular field.
    /// </summary>
    public Task<OrganizationProfileDto> PatchProfileAsync(
        PatchOrganizationProfileRequestDto request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        return WriteProfileAsync(
            current => new UpdateOrganizationProfileRequestDto(
                request.Product ?? current?.Product,
                request.Icp ?? current?.Icp,
                request.Objections ?? current?.Objections,
                request.ScriptStages ?? current?.ScriptStages,
                request.Tone ?? current?.Tone,
                request.Glossary ?? current?.Glossary,
                request.BannedClaims ?? current?.BannedClaims),
            cancellationToken);
    }

    /// <summary>
    /// Phase 40.29. What promoting a draft would do, computed and discarded — this method writes
    /// nothing.
    ///
    /// <para>
    /// The plan is built with <b>every</b> overwritable field accepted, because the preview's job is to
    /// show the most the draft could do. Planning it against the caller's current selection instead
    /// would show fewer conflicts the more of them they had already agreed to, which is backwards: the
    /// conflicts are what the screen exists to display.
    /// </para>
    /// </summary>
    public async Task<OrganizationProfileDraftPreviewDto> PreviewDraftAsync(
        ExtractedProfileDraftDto draft, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(draft);

        var profile = await GetProfileAsync(cancellationToken);

        var plan = OrganizationProfileDraftMerger.Plan(
            profile, draft, OrganizationProfileFields.Overwritable);

        var conflictCount = plan.Proposals.Count(proposal =>
            proposal.Decision == OrganizationProfileFieldProposalDto.DecisionConflict);

        return new OrganizationProfileDraftPreviewDto(
            plan.Proposals,
            conflictCount,
            OrganizationProfileGapInspector.Inspect(ToProfileDto(profile, plan.Merged)));
    }

    /// <summary>
    /// Phase 40.29. «Перенести в профиль». The merge policy runs on the server and the whole write
    /// goes through <see cref="WriteProfileAsync"/>, so the promotion publishes
    /// <c>organization.profile.updated</c> like every other save and the two replicas of 40.19 learn
    /// about it the same way.
    ///
    /// <para>
    /// The plan is computed <b>inside</b> the write transaction, against the row as it is at that
    /// moment. Planning it outside and saving the result afterwards would let a save that landed in
    /// between be silently discarded — and on this row that means a banned claim somebody entered while
    /// the reviewer was reading the preview.
    /// </para>
    /// </summary>
    public async Task<OrganizationProfileDraftAppliedDto> ApplyDraftAsync(
        ApplyOrganizationProfileDraftRequestDto request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var draft = request.Draft
            ?? throw new OrganizationProfileValidationException(ErrorMessages.OrganizationProfileDraftRequired);

        IReadOnlyList<OrganizationProfileFieldProposalDto> appliedProposals = [];

        var profile = await WriteProfileAsync(
            current =>
            {
                var plan = OrganizationProfileDraftMerger.Plan(current, draft, request.AcceptedFields);
                appliedProposals = plan.Proposals;

                return plan.Merged;
            },
            cancellationToken);

        return new OrganizationProfileDraftAppliedDto(
            profile,
            appliedProposals,
            OrganizationProfileGapInspector.Inspect(profile));
    }

    /// <summary>
    /// The single write path of this aggregate. Everything that changes the profile row — the form,
    /// one interview answer, a promoted draft — goes through here, so all three read the row inside
    /// the transaction that then writes it, and all three publish the same event afterwards.
    ///
    /// <para>
    /// <b>Why the lookup has to be inside the transaction, not merely near it.</b> EF opens an implicit
    /// transaction for <c>SaveChangesAsync</c>, but this lookup runs before it — so under a
    /// <c>NOBYPASSRLS</c> role it would run with <c>app.organization_id</c> unset, return nothing, and
    /// send the method down the "create new" branch every time. The <c>INSERT</c> then collides with the
    /// row that was there all along, and an organization can never edit its profile twice. Sharing one
    /// transaction also makes the upsert itself atomic.
    /// </para>
    /// </summary>
    /// <param name="buildRequest">
    /// Given the profile as it is inside the transaction (or <see langword="null"/> when there is
    /// none), returns the profile as it should be. Called exactly once.
    /// </param>
    private async Task<OrganizationProfileDto> WriteProfileAsync(
        Func<OrganizationProfileDto?, UpdateOrganizationProfileRequestDto> buildRequest,
        CancellationToken cancellationToken)
    {
        var currentOrganizationId = RequireOrganizationId();
        var now = DateTime.UtcNow;

        await using var transaction = await databaseContext.Database.BeginTransactionAsync(cancellationToken);

        var profile = await databaseContext.OrganizationProfiles
            .FirstOrDefaultAsync(candidate => candidate.OrganizationId == currentOrganizationId, cancellationToken);

        var request = buildRequest(profile is null ? null : ToDto(profile));

        if (profile is null)
        {
            profile = new OrganizationProfile
            {
                OrganizationId = currentOrganizationId,
                CreatedAt = now,
            };
            databaseContext.OrganizationProfiles.Add(profile);
        }

        profile.Product = request.Product;
        profile.Icp = request.Icp;
        profile.ObjectionsJson = JsonSerializer.Serialize(request.Objections ?? [], SerializerOptions);
        profile.ScriptJson = JsonSerializer.Serialize(request.ScriptStages ?? [], SerializerOptions);
        profile.Tone = request.Tone;
        profile.GlossaryJson = JsonSerializer.Serialize(request.Glossary ?? new Dictionary<string, string>(), SerializerOptions);
        profile.BannedClaimsJson = JsonSerializer.Serialize(request.BannedClaims ?? [], SerializerOptions);
        profile.UpdatedAt = now;

        await databaseContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        await PublishProfileUpdatedAsync(profile, cancellationToken);

        return ToDto(profile);
    }

    /// <summary>
    /// Announces a saved profile to the read-only replicas learning-service and ai-service keep of this
    /// row (Phase 40.19, docs/TENANCY/BACKGROUND_JOBS.md).
    ///
    /// <para>
    /// <b>Called after the commit, never inside it.</b> A replica that learned about a profile the
    /// transaction then rolled back would render a lesson with text no organization ever saved. The
    /// other direction — a commit whose event is lost — is the one the payload is designed for: the
    /// whole profile ships every time, so the next save repairs it, and until then the reader falls
    /// back to the neutral base wording.
    /// </para>
    /// </summary>
    private Task PublishProfileUpdatedAsync(OrganizationProfile profile, CancellationToken cancellationToken)
        => eventPublisher.PublishAsync(
            Topics.OrganizationProfileUpdated,
            profile.OrganizationId.ToString(),
            Topics.OrganizationProfileUpdated,
            new OrganizationProfileUpdatedEvent(
                profile.OrganizationId,
                profile.Product,
                profile.Icp,
                profile.Tone,
                profile.ObjectionsJson,
                profile.ScriptJson,
                profile.GlossaryJson,
                profile.BannedClaimsJson,
                profile.UpdatedAt),
            organizationId: profile.OrganizationId,
            cancellationToken: cancellationToken);

    private Guid RequireOrganizationId()
        => tenantContext.OrganizationId ?? throw new InvalidOperationException(ErrorMessages.OrganizationProfileContextMissing);

    /// <summary>
    /// The profile a merge plan describes, without going back to the database. Used by the preview,
    /// which has to answer «какие вопросы останутся» about a row that does not exist yet.
    /// </summary>
    private static OrganizationProfileDto ToProfileDto(
        OrganizationProfileDto? current, UpdateOrganizationProfileRequestDto merged) => new(
        merged.Product,
        merged.Icp,
        merged.Objections ?? [],
        merged.ScriptStages ?? [],
        merged.Tone,
        merged.Glossary ?? new Dictionary<string, string>(),
        merged.BannedClaims ?? [],
        current?.CreatedAt ?? DateTime.UtcNow,
        current?.UpdatedAt ?? DateTime.UtcNow);

    private static OrganizationProfileDto ToDto(OrganizationProfile profile) => new(
        profile.Product,
        profile.Icp,
        JsonSerializer.Deserialize<List<OrganizationObjectionDto>>(profile.ObjectionsJson, SerializerOptions) ?? [],
        JsonSerializer.Deserialize<List<string>>(profile.ScriptJson, SerializerOptions) ?? [],
        profile.Tone,
        JsonSerializer.Deserialize<Dictionary<string, string>>(profile.GlossaryJson, SerializerOptions) ?? new Dictionary<string, string>(),
        JsonSerializer.Deserialize<List<string>>(profile.BannedClaimsJson, SerializerOptions) ?? [],
        profile.CreatedAt,
        profile.UpdatedAt);
}
