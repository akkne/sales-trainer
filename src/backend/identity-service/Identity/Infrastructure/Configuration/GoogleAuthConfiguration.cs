namespace Sellevate.Identity.Infrastructure.Configuration;

public sealed class GoogleAuthConfiguration
{
    public const string SectionName = "Google";

    public required string ClientId { get; init; }
}
