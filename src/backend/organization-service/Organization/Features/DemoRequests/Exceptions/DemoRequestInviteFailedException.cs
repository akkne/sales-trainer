namespace Sellevate.Organization.Features.DemoRequests.Exceptions;

/// <summary>
/// <c>identity-service</c>'s bootstrap-admin call could not be completed for any reason other than the
/// two it reports on purpose (bad role/email, or an organization that already has an administrator) —
/// a timeout, an unreachable host, an unexpected status code. Rendered as <c>503</c>: the organization
/// already exists and is committed, the lead is left at <see cref="Models
/// .DemoRequestProvisioningState.OrganizationCreated"/>, and calling <c>/provision</c> again is always
/// safe and is expected to be exactly how this resolves.
/// </summary>
public sealed class DemoRequestInviteFailedException(Guid organizationId) : Exception(
    $"Could not bootstrap an administrator for organization '{organizationId}'.")
{
    public Guid OrganizationId { get; } = organizationId;
}
