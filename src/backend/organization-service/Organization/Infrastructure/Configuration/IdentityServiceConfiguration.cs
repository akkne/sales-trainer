namespace Sellevate.Organization.Infrastructure.Configuration;

/// <summary>
/// Where <c>identity-service</c> lives, for the one call this service makes to it: minting the
/// bootstrap administrator invite that demo-request provisioning needs.
/// </summary>
public sealed class IdentityServiceConfiguration
{
    public const string SectionName = "IdentityService";

    public required string BaseUrl { get; init; }

    public string BootstrapAdministratorPathTemplate { get; init; } = "/internal/organizations/{0}/bootstrap-admin";

    /// <summary>
    /// How long the bootstrap call may take before provisioning gives up and leaves the lead at
    /// <c>OrganizationCreated</c> for a retry. Short on purpose: this sits inside a platform
    /// superadmin pressing "provision", and a call that hangs is worse than one that fails fast and
    /// can be pressed again.
    /// </summary>
    public int TimeoutSeconds { get; init; } = 10;
}
