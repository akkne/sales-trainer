namespace Sellevate.Ai.Identity;

/// <summary>
/// A projection of identity-service's user, kept so a dialog transcript can name its author without a
/// cross-service call.
///
/// <para>
/// <b>Deliberately platform-scoped, not tenant-scoped</b> — it carries no <c>OrganizationId</c> and is
/// not covered by a query filter. A user is a platform identity that may hold memberships in several
/// organizations, and the replica exists only to resolve a display name, so scoping it per tenant would
/// duplicate a row per membership and still answer the same question. See <c>docs/DECISIONS.md</c>; do
/// not "fix" it.
/// </para>
/// </summary>
public sealed class UserReplica
{
    public Guid UserId { get; set; }
    public string Email { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string? AvatarKey { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
