namespace Sellevate.BuildingBlocks.Tests.Tenancy.Support;

/// <summary>
/// A row that belongs to no organization — the stand-in for identity-service's <c>Users</c> and
/// <c>RefreshTokens</c>, which are written on requests that legitimately carry no tenant context.
/// </summary>
internal sealed class PlatformGlobalTestEntity
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;
}
