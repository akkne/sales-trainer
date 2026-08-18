namespace Sellevate.Organization.Features.Organizations.Models;

/// <summary>
/// Creates a new tenant registry row. Deliberately carries no organization id — the platform
/// mints <see cref="Models.Organization.Id"/> itself (docs/TENANCY/TENANCY.md §1.3: an
/// organization identifier is never accepted from the caller, and this is doubly true for the
/// row that IS the tenant registry).
/// </summary>
public sealed record CreateOrganizationRequestDto(string Name, string? Slug);
