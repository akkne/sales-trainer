namespace Sellevate.Organization.Common.Constants;

/// <summary>
/// The messages this service puts in a response body or an exception. Held here rather than at the
/// throw site so the wording a customer sees cannot drift between two paths that fail for the same
/// reason.
/// </summary>
public static class ErrorMessages
{
    /// <summary>
    /// Thrown, not returned: no route can reach the profile without <c>TenantScoped</c> having already
    /// rejected a request with no organization on it, so reaching this is a wiring fault rather than a
    /// caller's mistake.
    /// </summary>
    public const string OrganizationProfileContextMissing = "Organization context is not set.";

    /// <summary>Phase 40.29. An apply request with no draft in it has nothing to promote.</summary>
    public const string OrganizationProfileDraftRequired = "draft is required.";
}
