namespace Sellevate.Identity.Infrastructure.Configuration;

/// <summary>
/// Signing and lifetime settings for every token this service mints. <c>Key</c> is a secret and comes
/// from the environment; Program.cs refuses to start unless it is at least 32 bytes, which HMAC-SHA256
/// requires. The access-token lifetime is short because a refresh token renews it;
/// <c>RefreshTokenLifetimeDays</c> is therefore the real session length.
/// </summary>
public sealed class JwtConfiguration
{
    public const string SectionName = "Jwt";

    public required string Key { get; init; }
    public required string Issuer { get; init; }
    public required string Audience { get; init; }
    public int AccessTokenLifetimeMinutes { get; init; } = 15;
    public int RefreshTokenLifetimeDays { get; init; } = 30;
    public int DemoTokenLifetimeHours { get; init; } = 2;
}
