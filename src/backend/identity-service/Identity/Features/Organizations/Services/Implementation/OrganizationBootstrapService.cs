using Microsoft.EntityFrameworkCore;
using Sellevate.BuildingBlocks.Tenancy;
using Sellevate.Identity.Features.Auth.Models;
using Sellevate.Identity.Features.Invites.Models;
using Sellevate.Identity.Features.Invites.Services.Abstract;
using Sellevate.Identity.Features.Membership.Models;
using Sellevate.Identity.Features.Organizations.Constants;
using Sellevate.Identity.Features.Organizations.Exceptions;
using Sellevate.Identity.Features.Organizations.Models;
using Sellevate.Identity.Features.Organizations.Services.Abstract;
using Sellevate.Identity.Features.PlatformAdmin.Services.Implementation;
using Sellevate.Identity.Infrastructure.Data;

namespace Sellevate.Identity.Features.Organizations.Services.Implementation;

/// <summary>
/// The identity-side half of demo-request provisioning (see docs/DECISIONS.md and
/// docs/ARCHITECTURE.md's service-to-service HTTP table). Reached only through
/// <c>InternalOrganizationBootstrapController</c>, guarded by the shared internal-service secret
/// rather than a JWT — the caller is organization-service itself, not a browser.
///
/// <para>
/// <b>The replica upsert at the top is load-bearing, not defensive.</b> Every other consumer of
/// <see cref="OrganizationReplica"/> waits for Kafka's <c>organization.created</c>, and
/// docs/API_CONTRACTS.md already documents the resulting gap for the JWT-facing
/// <c>bootstrap-admin</c> route: a <c>404</c> can legitimately mean "organization-service created it
/// seconds ago and identity-service has not consumed the event yet," which is tolerable for a human
/// retrying a form. It is not tolerable here: this call is issued by organization-service itself, in
/// the same request that just committed the organization row, so it would race its own Kafka consumer
/// on every single provision. The caller passing this payload directly *is* the registry owner, so
/// the payload is authoritative and the replica is written from it rather than waited for.
/// </para>
///
/// <para>
/// <b>The actor is re-checked against identity-db, never trusted from the request.</b> The shared
/// secret authorizes organization-service's channel; <see cref="InternalBootstrapAdministratorRequestDto
/// .ActorUserId"/> only attributes who is acting. Skipping this check would let any caller holding the
/// secret claim to act as any platform SuperAdmin — including laundering a plain <c>Admin</c>'s own
/// provisioning click into a superadmin act, which is exactly the boundary
/// <c>AuthorizationPolicies</c> draws between the two ranks.
/// </para>
/// </summary>
internal sealed class OrganizationBootstrapService(
    IdentityDbContext databaseContext,
    IServiceScopeFactory serviceScopeFactory,
    ILogger<OrganizationBootstrapService> logger) : IOrganizationBootstrapService
{
    public async Task<InternalBootstrapAdministratorResponseDto> BootstrapAdministratorAsync(
        Guid organizationId,
        InternalBootstrapAdministratorRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        await UpsertOrganizationReplicaAsync(organizationId, request, cancellationToken);

        var actor = await databaseContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(user => user.Id == request.ActorUserId, cancellationToken);

        if (actor is null || actor.Role != UserRole.SuperAdmin)
        {
            throw new OrganizationBootstrapOperationException(
                OrganizationBootstrapRejectionReason.ActorNotAuthorized,
                OrganizationBootstrapConstants.ActorNotAuthorizedMessage);
        }

        using var organizationScope = serviceScopeFactory.CreateScope();
        var scopedTenantContext = organizationScope.ServiceProvider.GetRequiredService<TenantContext>();
        scopedTenantContext.SetOrganization(organizationId);
        var scopedDatabaseContext = organizationScope.ServiceProvider.GetRequiredService<IdentityDbContext>();

        var (hasActiveAdministrator, pendingInvite) =
            await InspectExistingBootstrapStateAsync(scopedDatabaseContext, organizationId, cancellationToken);

        if (hasActiveAdministrator)
        {
            throw new OrganizationBootstrapOperationException(
                OrganizationBootstrapRejectionReason.ActiveAdministratorExists,
                OrganizationBootstrapConstants.ActiveAdministratorExistsMessage);
        }

        if (pendingInvite is not null)
        {
            return ToResponse(pendingInvite);
        }

        var requestedRole = PlatformAdminService.ResolveBootstrapRole(request.Role);
        var inviteService = organizationScope.ServiceProvider.GetRequiredService<IInviteService>();

        CreateInvitesResponseDto createInvitesResponse;
        try
        {
            createInvitesResponse = await inviteService.CreateAsync(
                new CreateInvitesRequestDto(request.Email, Emails: null, Role: requestedRole.ToString()),
                request.ActorUserId,
                cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Bootstrap invite mail send failed for organization {OrganizationId} after the invite was committed; "
                + "recovering the committed invite instead of surfacing a 500",
                organizationId);

            var recoveredInvite = await FindPendingAdministratorInviteAsync(scopedDatabaseContext, cancellationToken);
            if (recoveredInvite is null)
            {
                throw;
            }

            return ToResponse(recoveredInvite);
        }

        if (createInvitesResponse.Created.Count == 0)
        {
            var rejectionReason = createInvitesResponse.Rejected.Count > 0
                ? createInvitesResponse.Rejected[0].Reason
                : "unknown";
            throw new ArgumentException($"The address could not be invited: {rejectionReason}.", nameof(request));
        }

        var createdInvite = createInvitesResponse.Created[0];

        logger.LogWarning(
            "Demo-request bootstrap invited {Role} InviteId={InviteId} OrganizationId={OrganizationIdentifier} "
            + "ActorUserId={ActorUserId}",
            requestedRole, createdInvite.Id, organizationId, request.ActorUserId);

        return new InternalBootstrapAdministratorResponseDto(createdInvite.Id, createdInvite.Email, createdInvite.ExpiresAt);
    }

    private async Task UpsertOrganizationReplicaAsync(
        Guid organizationId, InternalBootstrapAdministratorRequestDto request, CancellationToken cancellationToken)
    {
        var existingReplica = await databaseContext.OrganizationReplicas
            .FindAsync([organizationId], cancellationToken);

        if (existingReplica is null)
        {
            databaseContext.OrganizationReplicas.Add(new OrganizationReplica
            {
                OrganizationId = organizationId,
                Name = request.OrganizationName,
                Slug = request.OrganizationSlug,
                Status = OrganizationReplicaStatus.Active,
                UpdatedAt = DateTime.UtcNow,
            });
        }
        else
        {
            existingReplica.Name = request.OrganizationName;
            existingReplica.Slug = request.OrganizationSlug;
            existingReplica.Status = OrganizationReplicaStatus.Active;
            existingReplica.UpdatedAt = DateTime.UtcNow;
        }

        await databaseContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Reads inside an explicit transaction for the reason <c>PlatformAdminService
    /// .EnsureNoPendingOrganizationAdminInviteAsync</c> already documents: <c>Invites</c> is tenant-scoped
    /// and row-level security's <c>SET LOCAL app.organization_id</c> only fires on a transaction, so a
    /// bare <c>SELECT</c> against it would fail closed and answer "no pending invite" for an
    /// organization that has one.
    /// </summary>
    private static async Task<(bool HasActiveAdministrator, Invite? PendingInvite)> InspectExistingBootstrapStateAsync(
        IdentityDbContext scopedDatabaseContext, Guid organizationId, CancellationToken cancellationToken)
    {
        await using var transaction = await scopedDatabaseContext.Database.BeginTransactionAsync(cancellationToken);

        var hasActiveAdministrator = await scopedDatabaseContext.Memberships
            .AsNoTracking()
            .AnyAsync(
                membership => membership.OrganizationId == organizationId
                    && (membership.Role == OrgRole.TenancyAdmin || membership.Role == OrgRole.TenancySuperAdmin)
                    && membership.Status == MembershipStatus.Active,
                cancellationToken);

        Invite? pendingInvite = hasActiveAdministrator
            ? null
            : await ReadPendingAdministratorInviteAsync(scopedDatabaseContext, cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return (hasActiveAdministrator, pendingInvite);
    }

    private static async Task<Invite?> FindPendingAdministratorInviteAsync(
        IdentityDbContext scopedDatabaseContext, CancellationToken cancellationToken)
    {
        await using var transaction = await scopedDatabaseContext.Database.BeginTransactionAsync(cancellationToken);
        var pendingInvite = await ReadPendingAdministratorInviteAsync(scopedDatabaseContext, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return pendingInvite;
    }

    private static Task<Invite?> ReadPendingAdministratorInviteAsync(
        IdentityDbContext scopedDatabaseContext, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        return scopedDatabaseContext.Invites
            .Where(
                invite => (invite.Role == OrgRole.TenancyAdmin || invite.Role == OrgRole.TenancySuperAdmin)
                    && invite.AcceptedAt == null
                    && invite.RevokedAt == null
                    && invite.ExpiresAt > now)
            .OrderByDescending(invite => invite.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private static InternalBootstrapAdministratorResponseDto ToResponse(Invite invite)
        => new(invite.Id, invite.Email, invite.ExpiresAt);
}
