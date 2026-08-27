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
///
/// <para>
/// <b>An organization that already has administrators is not refused.</b> This route used to answer
/// <c>409</c> for one, which made provisioning a lead into an existing organization — a second РОП,
/// a replacement for one who left — impossible from the panel. The reasoning is on
/// <c>PlatformAdminService.BootstrapOrganizationAdminAsync</c>; what remains here is the retry
/// convergence, now keyed on the invited address rather than on the organization.
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

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        var pendingInvite = await FindPendingAdministratorInviteAsync(
            scopedDatabaseContext, normalizedEmail, cancellationToken);

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

            var recoveredInvite = await FindPendingAdministratorInviteAsync(
                scopedDatabaseContext, normalizedEmail, cancellationToken);
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
    /// The pending invite this route converges a retry onto, matched **on the invited address**, not
    /// on "does this organization have any administrator invite at all". The organization may have as
    /// many administrators as it likes (see <c>PlatformAdminService.BootstrapOrganizationAdminAsync</c>
    /// for why that cap is gone), so the only thing a second call for a *different* address may mean
    /// is a second administrator, and answering it with somebody else's invite would send the lead the
    /// wrong person's link. A demo request's <c>BootstrapAdminEmail</c> is resolved once and never
    /// re-derived, so a genuine retry always arrives with the same address it did the first time.
    ///
    /// <para>
    /// Reads inside an explicit transaction because <c>Invites</c> is tenant-scoped and row-level
    /// security's <c>SET LOCAL app.organization_id</c> only fires on a transaction — a bare
    /// <c>SELECT</c> would fail closed and answer "no pending invite" for an organization that has one
    /// (docs/TENANCY/TENANCY.md §1.5).
    /// </para>
    /// </summary>
    private static async Task<Invite?> FindPendingAdministratorInviteAsync(
        IdentityDbContext scopedDatabaseContext, string normalizedEmail, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        await using var transaction = await scopedDatabaseContext.Database.BeginTransactionAsync(cancellationToken);

        var pendingInvite = await scopedDatabaseContext.Invites
            .Where(
                invite => invite.Email == normalizedEmail
                    && (invite.Role == OrgRole.TenancyAdmin || invite.Role == OrgRole.TenancySuperAdmin)
                    && invite.AcceptedAt == null
                    && invite.RevokedAt == null
                    && invite.ExpiresAt > now)
            .OrderByDescending(invite => invite.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return pendingInvite;
    }

    private static InternalBootstrapAdministratorResponseDto ToResponse(Invite invite)
        => new(invite.Id, invite.Email, invite.ExpiresAt);
}
