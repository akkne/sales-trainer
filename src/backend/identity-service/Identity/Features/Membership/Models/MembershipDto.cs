namespace Sellevate.Identity.Features.Membership.Models;

/// <summary>
/// One person in the caller's organization, as the organization's own admin panel needs to see them:
/// who they are, what they may do, and whether they are still working here.
///
/// <para>
/// <see cref="Role"/> and <see cref="Status"/> are strings rather than the enums they come from,
/// because identity-service does not register <c>JsonStringEnumConverter</c> and a numeric role on
/// the wire is a contract nobody can read — the same reason <c>CreatedInviteDto.Role</c> is a string.
/// </para>
///
/// <para>
/// Email and display name come from the platform-global <c>User</c> table, which has no
/// organization column: the tenant boundary on this endpoint is the membership row, not the user
/// row, so the query starts from <c>Memberships</c> and joins outward. Starting from <c>Users</c>
/// would enumerate the whole installation.
/// </para>
/// </summary>
public sealed record MembershipDto(
    Guid UserId,
    string Email,
    string DisplayName,
    string Role,
    string Status,
    DateTime JoinedAt,
    DateTime? DeactivatedAt);
