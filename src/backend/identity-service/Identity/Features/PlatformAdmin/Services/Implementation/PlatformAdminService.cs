using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Sellevate.BuildingBlocks.Tenancy;
using Sellevate.Identity.Common.Constants;
using Sellevate.Identity.Features.Auth.Models;
using Sellevate.Identity.Features.Invites.Models;
using Sellevate.Identity.Features.Invites.Services.Abstract;
using Sellevate.Identity.Features.Invites.Services.Implementation;
using Sellevate.Identity.Features.Membership.Models;
using Sellevate.Identity.Features.Organizations.Models;
using Sellevate.Identity.Features.PlatformAdmin.Constants;
using Sellevate.Identity.Features.PlatformAdmin.Exceptions;
using Sellevate.Identity.Features.PlatformAdmin.Models;
using Sellevate.Identity.Features.PlatformAdmin.Services.Abstract;
using Sellevate.Identity.Infrastructure.Configuration;
using Sellevate.Identity.Infrastructure.Data;

namespace Sellevate.Identity.Features.PlatformAdmin.Services.Implementation;

/// <summary>
/// The platform-superadmin operations that need identity-db: minting impersonation tokens, reading
/// the audit trail, and inviting an organization's administrators.
///
/// <para>
/// An impersonation audit row is written and committed <em>before</em> the token is handed back, so a
/// token that exists always has a record behind it. The reverse order would let a crash between the
/// two produce an unaudited session.
/// </para>
///
/// <para>
/// Bootstrapping runs the invite in its own DI scope with its own <see cref="TenantContext"/> pointed
/// at the target organization. The request scope belongs to the superadmin and may already be pinned
/// to a different organization (or to none), and <see cref="TenantContext"/> refuses to be re-pointed
/// once set — deliberately. That is the same "open a scope per unit of work" shape background jobs
/// use (docs/TENANCY/TENANCY.md §1.6), and it means the invite is created by the ordinary Phase 40.7
/// <see cref="IInviteService"/> with the ordinary tenant guards active, not by a second, parallel
/// invite path.
/// </para>
/// </summary>
internal sealed class PlatformAdminService(
    IdentityDbContext databaseContext,
    IServiceScopeFactory serviceScopeFactory,
    IOptions<JwtConfiguration> jwtOptions,
    IOptions<ImpersonationConfiguration> impersonationOptions,
    ILogger<PlatformAdminService> logger) : IPlatformAdminService
{
    public async Task<ImpersonationTokenDto> StartImpersonationAsync(
        CreateImpersonationRequestDto request,
        PlatformAdminActor actor,
        CancellationToken cancellationToken = default)
    {
        if (actor.IsAlreadyImpersonating)
        {
            throw new PlatformAdminOperationException(
                PlatformAdminRejectionReason.ImpersonationChainingForbidden,
                PlatformAdminConstants.ImpersonationChainingForbiddenMessage);
        }

        var organization = await RequireActiveOrganizationAsync(request.OrganizationId, cancellationToken);

        var issuedAt = DateTime.UtcNow;
        var expiresAt = issuedAt.AddMinutes(impersonationOptions.Value.TokenLifetimeMinutes);

        var auditEntry = new ImpersonationAuditEntry
        {
            Id = Guid.NewGuid(),
            ActorUserId = actor.UserId,
            ActorEmail = actor.Email,
            OrganizationId = organization.OrganizationId,
            OrganizationName = organization.Name,
            Reason = request.Reason.Trim(),
            IssuedAt = issuedAt,
            ExpiresAt = expiresAt,
        };

        databaseContext.ImpersonationAuditEntries.Add(auditEntry);
        await databaseContext.SaveChangesAsync(cancellationToken);

        var accessToken = BuildImpersonationAccessToken(actor, organization.OrganizationId, auditEntry.Id, expiresAt);

        logger.LogWarning(
            "Impersonation started ImpersonationId={ImpersonationId} ActorUserId={ActorUserId} "
            + "OrganizationId={OrganizationIdentifier} ExpiresAt={ExpiresAt}",
            auditEntry.Id, actor.UserId, organization.OrganizationId, expiresAt);

        return new ImpersonationTokenDto(
            accessToken,
            expiresAt,
            auditEntry.Id,
            new OrganizationReferenceDto(organization.OrganizationId, organization.Name));
    }

    public async Task<IReadOnlyList<ImpersonationAuditEntryDto>> ListImpersonationsAsync(
        CancellationToken cancellationToken = default)
    {
        var auditEntries = await databaseContext.ImpersonationAuditEntries
            .AsNoTracking()
            .OrderByDescending(entry => entry.IssuedAt)
            .Take(PlatformAdminConstants.AuditPageSize)
            .ToListAsync(cancellationToken);

        return auditEntries
            .Select(entry => new ImpersonationAuditEntryDto(
                entry.Id,
                entry.ActorUserId,
                entry.ActorEmail,
                new OrganizationReferenceDto(entry.OrganizationId, entry.OrganizationName),
                entry.Reason,
                entry.IssuedAt,
                entry.ExpiresAt))
            .ToList();
    }

    /// <summary>
    /// Invites an administrator into the named organization. **There is no cap on how many an
    /// organization may have**, at either rank: the 40.9 shape allowed exactly one and refused every
    /// later call as "already bootstrapped", which made a company with two РОПs — or one whose only
    /// administrator left — a support ticket rather than a form. Duplicate addresses are still
    /// refused, but by <see cref="IInviteService.CreateAsync"/>'s ordinary
    /// <c>already-a-member</c> / <c>invite-already-pending</c> rejections, which every invite obeys.
    ///
    /// <para>
    /// The back-door concern the old cap answered is unchanged and is answered elsewhere: this route
    /// is <c>RequireSuperAdmin</c>, and platform staff can already reach any running organization
    /// through <see cref="StartImpersonationAsync"/> — which, unlike this, writes an audit row. The
    /// cap never removed that reach; it only removed the ability to staff a customer properly.
    /// </para>
    /// </summary>
    public async Task<BootstrapOrganizationAdminResponseDto> BootstrapOrganizationAdminAsync(
        BootstrapOrganizationAdminRequestDto request,
        PlatformAdminActor actor,
        CancellationToken cancellationToken = default)
    {
        var requestedRole = ResolveBootstrapRole(request.Role);

        var organization = await RequireActiveOrganizationAsync(request.OrganizationId, cancellationToken);

        using var organizationScope = serviceScopeFactory.CreateScope();
        var scopedTenantContext = organizationScope.ServiceProvider.GetRequiredService<TenantContext>();
        scopedTenantContext.SetOrganization(organization.OrganizationId);

        var inviteService = organizationScope.ServiceProvider.GetRequiredService<IInviteService>();
        var createInvitesResponse = await inviteService.CreateAsync(
            new CreateInvitesRequestDto(request.Email, Emails: null, Role: requestedRole.ToString()),
            actor.UserId,
            cancellationToken);

        if (createInvitesResponse.Created.Count == 0)
        {
            var rejectionReason = createInvitesResponse.Rejected.Count > 0
                ? createInvitesResponse.Rejected[0].Reason
                : "unknown";
            throw new ArgumentException($"The address could not be invited: {rejectionReason}.", nameof(request));
        }

        var createdInvite = createInvitesResponse.Created[0];

        logger.LogWarning(
            "Platform-invited {Role} InviteId={InviteId} OrganizationId={OrganizationIdentifier} "
            + "ActorUserId={ActorUserId}",
            requestedRole, createdInvite.Id, organization.OrganizationId, actor.UserId);

        return new BootstrapOrganizationAdminResponseDto(
            createdInvite.Id,
            new OrganizationReferenceDto(organization.OrganizationId, organization.Name),
            createdInvite.Email,
            createdInvite.ExpiresAt,
            createdInvite.Token);
    }

    /// <summary>
    /// Defaults an omitted or blank <see cref="BootstrapOrganizationAdminRequestDto.Role"/> to
    /// <see cref="OrgRole.TenancySuperAdmin"/> so callers that predate the field keep working, then
    /// parses it through <see cref="InviteService.ParseRole"/> — the same rules an ordinary invite
    /// obeys, including the retired <c>OrgAdmin</c> name check — and finally narrows the result to
    /// <see cref="OrgRole.TenancyAdmin"/> or <see cref="OrgRole.TenancySuperAdmin"/>. That last step
    /// is what keeps this platform endpoint from minting a <see cref="OrgRole.Manager"/> or any
    /// other organization role: it may only choose which rank of administrator it bootstraps.
    ///
    /// <para>
    /// <c>internal</c> rather than <c>private</c> so <c>Organizations.Services.Implementation
    /// .OrganizationBootstrapService</c> — the machine-to-machine bootstrap route demo-request
    /// provisioning calls — applies exactly this narrowing instead of carrying a second definition of
    /// which roles a bootstrap call may mint, the same reasoning that made
    /// <see cref="InviteService.ParseRole"/> <c>internal</c> in the first place.
    /// </para>
    /// </summary>
    internal static OrgRole ResolveBootstrapRole(string? requestedRoleName)
    {
        var roleName = string.IsNullOrWhiteSpace(requestedRoleName)
            ? nameof(OrgRole.TenancySuperAdmin)
            : requestedRoleName;

        var parsedRole = InviteService.ParseRole(roleName);

        if (parsedRole is not (OrgRole.TenancyAdmin or OrgRole.TenancySuperAdmin))
        {
            throw new ArgumentException(
                $"Bootstrap admin role must be '{nameof(OrgRole.TenancyAdmin)}' or "
                + $"'{nameof(OrgRole.TenancySuperAdmin)}', but was '{roleName}'.",
                nameof(requestedRoleName));
        }

        return parsedRole;
    }

    private async Task<OrganizationReplica> RequireActiveOrganizationAsync(
        Guid organizationId,
        CancellationToken cancellationToken)
    {
        var organization = await databaseContext.OrganizationReplicas
            .AsNoTracking()
            .FirstOrDefaultAsync(replica => replica.OrganizationId == organizationId, cancellationToken);

        if (organization is null)
        {
            throw new PlatformAdminOperationException(
                PlatformAdminRejectionReason.OrganizationNotKnown,
                PlatformAdminConstants.OrganizationNotKnownMessage);
        }

        if (organization.Status == OrganizationReplicaStatus.Suspended)
        {
            throw new PlatformAdminOperationException(
                PlatformAdminRejectionReason.OrganizationSuspended,
                PlatformAdminConstants.OrganizationSuspendedMessage);
        }

        return organization;
    }

    /// <summary>
    /// Builds the impersonation token by hand rather than reusing <c>AuthenticationService</c>'s
    /// token path, because the differences are the security properties, not incidental details:
    ///
    /// <list type="bullet">
    /// <item>the platform role is downgraded to <see cref="UserRole.User"/>, so the token cannot
    /// reach any <c>RequireSuperAdmin</c> route — including this one, which is what stops an
    /// impersonation session from starting another;</item>
    /// <item>it carries the <see cref="ImpersonationClaimNames"/> marker claims, so it is
    /// recognisable as an impersonation token wherever it turns up;</item>
    /// <item>it is short-lived and has no refresh token, so it cannot be silently renewed.</item>
    /// </list>
    ///
    /// <para>
    /// <c>sub</c> stays the superadmin's own user id: the impersonator borrows an organization,
    /// never an identity. Anything the token writes is attributable to the real person.
    /// </para>
    /// </summary>
    private string BuildImpersonationAccessToken(
        PlatformAdminActor actor,
        Guid organizationId,
        Guid impersonationId,
        DateTime expiresAt)
    {
        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Value.Key));

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, actor.UserId.ToString()),
            new(JwtRegisteredClaimNames.Email, actor.Email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(ClaimTypeNames.DisplayName, actor.DisplayName),
            new(ClaimTypes.Role, UserRole.User.ToString()),
            new(ClaimTypeNames.OrganizationId, organizationId.ToString()),
            new(AuthorizationPolicies.OrganizationRoleClaimType, nameof(OrgRole.TenancyAdmin)),
            new(ImpersonationClaimNames.IsImpersonation, "true"),
            new(ImpersonationClaimNames.ImpersonationId, impersonationId.ToString()),
            new(ImpersonationClaimNames.ActorUserId, actor.UserId.ToString()),
        };

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = expiresAt,
            Issuer = jwtOptions.Value.Issuer,
            Audience = jwtOptions.Value.Audience,
            SigningCredentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256),
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        return tokenHandler.WriteToken(tokenHandler.CreateToken(tokenDescriptor));
    }
}
