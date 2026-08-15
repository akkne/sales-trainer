using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Sellevate.BuildingBlocks.Tenancy;
using Sellevate.Identity.Features.Auth.Models;
using Sellevate.Identity.Features.Invites.Models;
using Sellevate.Identity.Features.Invites.Services.Abstract;
using Sellevate.Identity.Features.Membership.Models;
using Sellevate.Identity.Features.Organizations.Models;
using Sellevate.Identity.Features.PlatformAdmin.Constants;
using Sellevate.Identity.Features.PlatformAdmin.Exceptions;
using Sellevate.Identity.Features.PlatformAdmin.Models;
using Sellevate.Identity.Features.PlatformAdmin.Services.Abstract;
using Sellevate.Identity.Infrastructure.Configuration;
using Sellevate.Identity.Infrastructure.Data;

namespace Sellevate.Identity.Features.PlatformAdmin.Services.Implementation;

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

        // The audit row is written and committed before the token is handed back, so a token that
        // exists always has a record behind it. The reverse order would allow a crash between the
        // two to produce an unaudited session.
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

    public async Task<BootstrapOrganizationAdminResponseDto> BootstrapOrganizationAdminAsync(
        BootstrapOrganizationAdminRequestDto request,
        PlatformAdminActor actor,
        CancellationToken cancellationToken = default)
    {
        var organization = await RequireActiveOrganizationAsync(request.OrganizationId, cancellationToken);

        var hasActiveOrganizationAdmin = await databaseContext.Memberships
            .AsNoTracking()
            .AnyAsync(
                membership => membership.OrganizationId == organization.OrganizationId
                    && membership.Role == OrgRole.OrgAdmin
                    && membership.Status == MembershipStatus.Active,
                cancellationToken);

        if (hasActiveOrganizationAdmin)
        {
            throw new PlatformAdminOperationException(
                PlatformAdminRejectionReason.OrganizationAlreadyBootstrapped,
                PlatformAdminConstants.OrganizationAlreadyBootstrappedMessage);
        }

        // Everything below runs in its own DI scope with its own TenantContext set to the target
        // organization. The request scope belongs to the superadmin and may already be pinned to a
        // different organization (or to none), and TenantContext refuses to be re-pointed once set
        // — deliberately. This is the same "open a scope per unit of work" shape background jobs
        // use (docs/TENANCY/TENANCY.md §1.6), and it means the invite is created by the ordinary
        // Phase 40.7 IInviteService with the ordinary tenant guards active, not by a second,
        // parallel invite path.
        using var organizationScope = serviceScopeFactory.CreateScope();
        var scopedTenantContext = organizationScope.ServiceProvider.GetRequiredService<TenantContext>();
        scopedTenantContext.SetOrganization(organization.OrganizationId);

        await EnsureNoPendingOrganizationAdminInviteAsync(organizationScope.ServiceProvider, cancellationToken);

        var inviteService = organizationScope.ServiceProvider.GetRequiredService<IInviteService>();
        var createInvitesResponse = await inviteService.CreateAsync(
            new CreateInvitesRequestDto(request.Email, Emails: null, Role: nameof(OrgRole.OrgAdmin)),
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
            "Bootstrap OrgAdmin invited InviteId={InviteId} OrganizationId={OrganizationIdentifier} "
            + "ActorUserId={ActorUserId}",
            createdInvite.Id, organization.OrganizationId, actor.UserId);

        return new BootstrapOrganizationAdminResponseDto(
            createdInvite.Id,
            new OrganizationReferenceDto(organization.OrganizationId, organization.Name),
            createdInvite.Email,
            createdInvite.ExpiresAt,
            createdInvite.Token);
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
    /// Refuses to bootstrap an organization that already has an unused <c>OrgAdmin</c> invite
    /// waiting. Runs in the target organization's own scope and inside an explicit transaction so
    /// the row-level-security <c>SET LOCAL</c> has something to attach to — a bare <c>SELECT</c>
    /// on a tenant-scoped table sees nothing otherwise (docs/TENANCY/TENANCY.md §1.5).
    /// </summary>
    private static async Task EnsureNoPendingOrganizationAdminInviteAsync(
        IServiceProvider scopedServices,
        CancellationToken cancellationToken)
    {
        var scopedDatabaseContext = scopedServices.GetRequiredService<IdentityDbContext>();
        var now = DateTime.UtcNow;

        await using var transaction = await scopedDatabaseContext.Database
            .BeginTransactionAsync(cancellationToken);

        var hasPendingOrganizationAdminInvite = await scopedDatabaseContext.Invites
            .AsNoTracking()
            .AnyAsync(
                invite => invite.Role == OrgRole.OrgAdmin
                    && invite.AcceptedAt == null
                    && invite.RevokedAt == null
                    && invite.ExpiresAt > now,
                cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        if (hasPendingOrganizationAdminInvite)
        {
            throw new PlatformAdminOperationException(
                PlatformAdminRejectionReason.OrganizationAlreadyBootstrapped,
                PlatformAdminConstants.OrganizationAlreadyBootstrappedMessage);
        }
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
            new("displayName", actor.DisplayName),
            new(ClaimTypes.Role, UserRole.User.ToString()),
            new("org_id", organizationId.ToString()),
            new("org_role", nameof(OrgRole.OrgAdmin)),
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
