namespace Sellevate.Organization.Infrastructure.Identity.Exceptions;

/// <summary>
/// <c>identity-service</c> reported that the target organization already has an active administrator
/// — the one outcome of the bootstrap-admin call that is a conflict rather than a transient failure,
/// so it is distinguished from every other non-success response instead of collapsing into a generic
/// invite-failed exception.
/// </summary>
public sealed class IdentityOrganizationBootstrapConflictException(Guid organizationId) : Exception(
    $"identity-service reports organization '{organizationId}' already has an administrator.")
{
    public Guid OrganizationId { get; } = organizationId;
}
