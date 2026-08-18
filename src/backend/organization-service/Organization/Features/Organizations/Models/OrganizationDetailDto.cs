namespace Sellevate.Organization.Features.Organizations.Models;

public sealed record OrganizationDetailDto(
    Guid Id,
    string Name,
    string Slug,
    string Status,
    DateTime CreatedAt,
    DateTime UpdatedAt);
