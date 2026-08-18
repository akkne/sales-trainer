using Sellevate.BuildingBlocks.Tenancy;

namespace Sellevate.BuildingBlocks.Tests.Tenancy.Support;

/// <summary>
/// Deliberately carries no EF query filter — <see cref="TenantRowLevelSecurityIntegrationTests"/>
/// exercises the raw Postgres boundary (Layer 3), not the EF convenience filter (Layer 2), so a
/// forgotten filter cannot accidentally make the test pass for the wrong reason.
/// </summary>
internal sealed class TenantRlsTestRow : ITenantScoped
{
    public Guid Id { get; set; }

    public Guid OrganizationId { get; set; }

    public string Name { get; set; } = string.Empty;
}
