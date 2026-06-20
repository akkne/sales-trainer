namespace Sellevate.Identity.Infrastructure.Configuration;

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
