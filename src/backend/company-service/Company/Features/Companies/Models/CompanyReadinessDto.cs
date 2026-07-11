namespace Sellevate.Company.Features.Companies.Models;

/// <summary>
/// Cached readiness score for a company. All fields are null when the company has no usable
/// data yet (no practice sessions, or ai-service found no usable feedback among them) — the
/// controller turns that into a 204.
/// </summary>
public sealed record CompanyReadinessDto(
    int? Score,
    IReadOnlyList<string>? Strengths,
    IReadOnlyList<string>? Gaps,
    string? Recommendation,
    DateTime? GeneratedAt);
