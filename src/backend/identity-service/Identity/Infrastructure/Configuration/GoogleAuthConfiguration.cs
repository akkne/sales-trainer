namespace Sellevate.Identity.Infrastructure.Configuration;

/// <summary>
/// The OAuth client id Google-issued identity tokens must be addressed to. Not a secret, but wrong
/// values are indistinguishable from a forged token, so it comes from the environment alongside the
/// real secrets rather than being committed.
/// </summary>
public sealed class GoogleAuthConfiguration
{
    public const string SectionName = "Google";

    public required string ClientId { get; init; }
}
