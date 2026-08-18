namespace Sellevate.Learning.Infrastructure.Configuration;

public sealed class IdentityServiceConfiguration
{
    public const string SectionName = "IdentityService";

    public required string BaseUrl { get; init; }

    public string ActiveMemberIdsPath { get; init; } = "/internal/memberships/active";

    /// <summary>
    /// How long the roster lookup may take before the issue attempt fails. Short on purpose: this
    /// call sits inside a РОП pressing "issue", and an issue that hangs is worse than one that
    /// refuses and can be pressed again.
    /// </summary>
    public int TimeoutSeconds { get; init; } = 10;
}
