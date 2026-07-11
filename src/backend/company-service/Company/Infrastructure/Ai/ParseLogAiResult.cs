namespace Sellevate.Company.Infrastructure.Ai;

public sealed record ParseLogAiResult(string? ContactName, string Subject, string Outcome, DateTime? OccurredAt);
