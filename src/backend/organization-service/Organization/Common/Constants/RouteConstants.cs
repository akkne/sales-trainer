namespace Sellevate.Organization.Common.Constants;

/// <summary>
/// Every route template this service serves, in one place, because these strings are a published
/// contract: the gateway forwards them by prefix and the frontend calls them by literal path, so a
/// segment renamed here is a broken deployment rather than a compile error.
/// </summary>
public static class RouteConstants
{
    public const string OrganizationsBase = "organizations";
    public const string OrganizationById = "{id:guid}";
    public const string SuspendOrganization = "{id:guid}/suspend";
    public const string ReactivateOrganization = "{id:guid}/reactivate";
    public const string OrganizationProfileBase = "organizations/profile";

    /// <summary>Relative to <see cref="OrganizationProfileBase"/>: what the interview asks next.</summary>
    public const string OrganizationProfileGaps = "gaps";

    /// <summary>
    /// Relative to <see cref="OrganizationProfileBase"/>: what promoting an extracted draft would do.
    /// Writes nothing — the writing half is <see cref="OrganizationProfileDraftApply"/>, deliberately a
    /// separate path rather than a flag on this one.
    /// </summary>
    public const string OrganizationProfileDraft = "draft";

    /// <summary>Relative to <see cref="OrganizationProfileBase"/>: commits a reviewed draft.</summary>
    public const string OrganizationProfileDraftApply = "draft/apply";

    /// <summary>The public, anonymous "Request a demo" lead-capture route.</summary>
    public const string DemoRequestsBase = "demo-requests";

    /// <summary>The platform-staff list of submitted demo requests.</summary>
    public const string AdminDemoRequestsBase = "admin/demo-requests";

    /// <summary>Relative to <see cref="AdminDemoRequestsBase"/>: records what happened after a lead
    /// was contacted.</summary>
    public const string AdminDemoRequestStatus = "{id:guid}/status";
}
