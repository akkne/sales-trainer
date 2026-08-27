namespace Sellevate.Identity.Features.Organizations.Constants;

/// <summary>
/// Wording of the internal organization-bootstrap surface. Each message pairs with a member of
/// <c>OrganizationBootstrapRejectionReason</c>, which is what the controller maps onto a status code.
/// </summary>
public static class OrganizationBootstrapConstants
{
    public const string ActorNotAuthorizedMessage =
        "The acting user is not a known platform SuperAdmin.";
}
