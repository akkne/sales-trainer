namespace Sellevate.Organization.Features.Organizations.Models;

/// <summary>
/// The tenant registry row itself — one per paying customer of Sellevate. Deliberately
/// NOT <c>ITenantScoped</c>: this table IS the tenant registry (docs/TENANCY/TENANCY.md §1.2,
/// §1.9) and is never filtered by an organization id, only ever addressed by its own
/// <see cref="Id"/>. Row-level security is never enabled on it — see docs/DECISIONS.md.
/// </summary>
public sealed class Organization
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Slug { get; set; } = string.Empty;

    public OrganizationStatus Status { get; set; } = OrganizationStatus.Active;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}
