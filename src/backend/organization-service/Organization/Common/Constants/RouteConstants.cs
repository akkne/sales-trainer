namespace Sellevate.Organization.Common.Constants;

public static class RouteConstants
{
    public const string OrganizationsBase = "organizations";
    public const string OrganizationById = "{id:guid}";
    public const string SuspendOrganization = "{id:guid}/suspend";
    public const string ReactivateOrganization = "{id:guid}/reactivate";
    public const string OrganizationProfileBase = "organizations/profile";
}
