namespace Sellevate.Organization.Features.DemoRequests.Models;

public sealed record DemoRequestDto(
    Guid Id,
    string FullName,
    string WorkEmail,
    string Phone,
    string CompanyName,
    string? JobTitle,
    string SalesTeamSize,
    string? Comment,
    string Status,
    DateTime ConsentGivenAt,
    DateTime? MarketingConsentGivenAt,
    DateTime CreatedAt,
    DateTime UpdatedAt);
