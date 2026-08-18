namespace Sellevate.Organization.Common.Constants;

public static class ErrorMessages
{
    public const string OrganizationNotFound = "Organization with the specified identifier was not found.";
    public const string OrganizationNameRequired = "Organization name is required.";
    public const string OrganizationProfileContextMissing = "Organization context is not set.";

    /// <summary>Phase 40.29. An apply request with no draft in it has nothing to promote.</summary>
    public const string OrganizationProfileDraftRequired = "draft is required.";
}
