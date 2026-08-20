namespace Sellevate.Identity.Features.Organizations.Exceptions;

public sealed class OrganizationBootstrapOperationException(OrganizationBootstrapRejectionReason reason, string message)
    : Exception(message)
{
    public OrganizationBootstrapRejectionReason Reason { get; } = reason;
}
