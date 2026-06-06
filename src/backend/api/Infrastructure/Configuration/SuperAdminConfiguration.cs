namespace SalesTrainer.Api.Infrastructure.Configuration;

public sealed class SuperAdminConfiguration
{
    public const string SectionName = "SuperAdmin";

    public required string Email { get; init; }
    public required string Password { get; init; }
    public required string DisplayName { get; init; }
}
