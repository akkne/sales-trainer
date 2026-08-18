namespace Sellevate.Identity.Infrastructure.Configuration;

/// <summary>
/// The platform superadministrator seeded on first startup. All three values are secrets supplied by
/// the environment — there is deliberately no default, because a committed default would be a
/// known-credentials account on every deployment.
/// </summary>
public sealed class SuperAdminConfiguration
{
    public const string SectionName = "SuperAdmin";

    public required string Email { get; init; }
    public required string Password { get; init; }
    public required string DisplayName { get; init; }
}
