namespace Sellevate.Organization.Features.Organizations.Models;

public sealed record OrganizationSummaryDto(
    Guid Id,
    string Name,
    string Slug,
    string Status,
    DateTime CreatedAt);
