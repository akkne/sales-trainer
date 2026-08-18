namespace Sellevate.Identity.Features.Auth.Exceptions;

/// <summary>
/// Thrown when tokens are about to be issued for a user whose organization has been suspended by
/// the platform (Phase 40.9). Raised at the single point where every login, Google sign-in, invite
/// acceptance and refresh converges, so suspension cannot be worked around by picking a different
/// entry route.
/// </summary>
public sealed class OrganizationSuspendedException(Guid organizationId)
    : Exception("This organization is suspended. Contact Sellevate support.")
{
    public Guid OrganizationId { get; } = organizationId;
}
