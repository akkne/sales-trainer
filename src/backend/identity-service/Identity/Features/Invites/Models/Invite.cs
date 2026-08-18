using Sellevate.BuildingBlocks.Tenancy;
using Sellevate.Identity.Features.Membership.Models;

namespace Sellevate.Identity.Features.Invites.Models;

/// <summary>
/// A single-use invitation to join one organization with one organization role. Since the product
/// has no public registration route (docs/TENANCY/TENANCY.md §4.1), this row is the only way a user
/// account or a <see cref="Membership.Models.Membership"/> ever comes into existence outside the
/// platform superadmin panel.
///
/// <para>
/// Only <see cref="TokenHash"/> is stored. The raw token is returned once, at creation, in the
/// response and in the invite email — a database dump therefore never yields a usable invite, the
/// same shape already used for <c>RefreshToken.Token</c> and <c>EmailVerificationCode.CodeHash</c>.
/// </para>
///
/// <para>
/// The row is tenant-scoped (<see cref="ITenantScoped"/>): an EF global query filter plus Postgres
/// row-level security keep one organization from ever seeing or revoking another's invites. The
/// acceptance path is the single deliberate exception and resolves its organization from the signed
/// token itself rather than from the request — see
/// <c>Services.Implementation.InviteService.AcceptAsync</c>.
/// </para>
/// </summary>
public sealed class Invite : ITenantScoped
{
    public Guid Id { get; set; }

    public Guid OrganizationId { get; set; }

    /// <summary>Lower-cased invitee address; matched against the globally unique <c>User.Email</c>.</summary>
    public string Email { get; set; } = string.Empty;

    public OrgRole Role { get; set; }

    public string TokenHash { get; set; } = string.Empty;

    public DateTime ExpiresAt { get; set; }

    public DateTime? AcceptedAt { get; set; }

    public DateTime? RevokedAt { get; set; }

    /// <summary>The user who issued the invite. Nullable so a platform-side bootstrap invite
    /// (the first <c>TenancySuperAdmin</c> of a brand new organization, 40.9) has somewhere to land.</summary>
    public Guid? InvitedBy { get; set; }

    public DateTime CreatedAt { get; set; }
}
