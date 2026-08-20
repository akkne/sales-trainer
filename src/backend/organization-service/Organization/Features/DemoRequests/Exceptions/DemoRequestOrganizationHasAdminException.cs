namespace Sellevate.Organization.Features.DemoRequests.Exceptions;

/// <summary>
/// <c>identity-service</c> refused to bootstrap an administrator because the target organization
/// already has an active one — most plausibly because a human used the manual escape hatch
/// (<c>POST /organizations</c> plus the JWT <c>bootstrap-admin</c> route) on this same organization
/// between a failed provision attempt and a retry. Rendered as <c>409</c>; the lead's own
/// provisioning state is left exactly where it was, since nothing about this organization or this
/// lead was written.
/// </summary>
public sealed class DemoRequestOrganizationHasAdminException(Guid organizationId) : Exception(
    $"Organization '{organizationId}' already has an administrator.")
{
    public Guid OrganizationId { get; } = organizationId;
}
