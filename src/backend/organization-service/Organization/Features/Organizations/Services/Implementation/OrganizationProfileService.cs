using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Sellevate.BuildingBlocks.Eventing;
using Sellevate.BuildingBlocks.Messaging;
using Sellevate.BuildingBlocks.Tenancy;
using Sellevate.Organization.Common.Constants;
using Sellevate.Organization.Eventing;
using Sellevate.Organization.Features.Organizations.Models;
using Sellevate.Organization.Features.Organizations.Services.Abstract;
using Sellevate.Organization.Infrastructure.Data;

namespace Sellevate.Organization.Features.Organizations.Services.Implementation;

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

        // A bare SELECT has no implicit EF transaction, so SET LOCAL app.organization_id (from
        // TenantConnectionInterceptor) has nothing to scope to unless one is opened explicitly —
        // see docs/TENANCY/TENANCY.md §1.5 and BuildingBlocks/Tenancy/TenantConnectionInterceptor.cs.
        await using var transaction = await databaseContext.Database.BeginTransactionAsync(cancellationToken);

        var profile = await databaseContext.OrganizationProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.OrganizationId == currentOrganizationId, cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return profile is null ? null : ToDto(profile);
    }

    public async Task<OrganizationProfileDto> UpsertProfileAsync(
        UpdateOrganizationProfileRequestDto request, CancellationToken cancellationToken = default)
    {
        var currentOrganizationId = RequireOrganizationId();
        var now = DateTime.UtcNow;

        // Same reason as GetProfileAsync above, and the consequence here is worse than a blank read.
        // EF opens an implicit transaction for SaveChangesAsync, but this lookup runs *before* it —
        // so under a NOBYPASSRLS role it would run with app.organization_id unset, return nothing,
        // and send the method down the "create new" branch every time. The INSERT then collides with
        // the row that was there all along, and an organization can never edit its profile twice.
        // Wrapping the read and the write in one transaction also makes the upsert actually atomic.
        await using var transaction = await databaseContext.Database.BeginTransactionAsync(cancellationToken);

        var profile = await databaseContext.OrganizationProfiles
            .FirstOrDefaultAsync(candidate => candidate.OrganizationId == currentOrganizationId, cancellationToken);

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

        // Published after the commit, not inside it: learning-service and ai-service keep read-only
        // replicas of this row (Phase 40.19, docs/TENANCY/BACKGROUND_JOBS.md), and a replica that
        // learned about a profile the transaction then rolled back would render a lesson with text
        // no organization ever saved. The other direction — a commit whose event is lost — is the
        // one the payload is designed for: the whole profile ships every time, so the next save
        // repairs it, and until then the reader falls back to the neutral base wording.
        await PublishProfileUpdatedAsync(profile, cancellationToken);

        return ToDto(profile);
    }

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
