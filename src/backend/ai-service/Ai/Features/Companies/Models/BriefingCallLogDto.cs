namespace Sellevate.Ai.Features.Companies.Models;

public sealed record BriefingCallLogDto(
    string? ContactName,
    string Subject,
    string Outcome,
    DateTime OccurredAt);
