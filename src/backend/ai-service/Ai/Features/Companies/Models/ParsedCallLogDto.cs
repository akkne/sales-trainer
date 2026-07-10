namespace Sellevate.Ai.Features.Companies.Models;

public sealed record ParsedCallLogDto(string? ContactName, string Subject, string Outcome, DateTime? OccurredAt);
