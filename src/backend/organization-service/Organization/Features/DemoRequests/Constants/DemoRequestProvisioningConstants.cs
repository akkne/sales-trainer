namespace Sellevate.Organization.Features.DemoRequests.Constants;

/// <summary>
/// Machine-readable error codes <c>POST /admin/demo-requests/{id}/provision</c> puts in an error
/// body's <c>code</c> field. Deliberately distinct from <see cref="Organizations.Exceptions
/// .OrganizationSlugConflictException.Code"/> even though a slug conflict is the same exception on
/// both routes: this endpoint's contract is documented as <c>slug-taken</c>, not
/// <c>organization_slug_conflict</c>, and the two response bodies are free to name the same failure
/// differently.
/// </summary>
public static class DemoRequestProvisioningConstants
{
    public const string SlugTakenCode = "slug-taken";
    public const string OrganizationHasAdminCode = "organization-has-admin";
    public const string InviteFailedCode = "invite-failed";

    public const string DefaultProvisioningRole = "TenancySuperAdmin";
}
