namespace Sellevate.Identity.Features.PlatformAdmin.Constants;

/// <summary>
/// Wording and limits of the platform-superadmin surface. Each message pairs with a member of
/// <c>PlatformAdminRejectionReason</c>, which is what the controller maps onto a status code.
/// </summary>
public static class PlatformAdminConstants
{
    public const string OrganizationNotKnownMessage =
        "This organization is not known to identity-service yet. If it was just created, wait for "
        + "the organization.created event to be consumed and try again.";

    public const string OrganizationSuspendedMessage =
        "This organization is suspended.";

    public const string ImpersonationChainingForbiddenMessage =
        "An impersonation token cannot start another impersonation.";

    /// <summary>Newest-first page size for the audit list. Small on purpose — the endpoint exists
    /// to answer "who went in recently", not to be an export.</summary>
    public const int AuditPageSize = 100;
}
