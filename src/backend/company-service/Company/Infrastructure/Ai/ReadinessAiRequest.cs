namespace Sellevate.Company.Infrastructure.Ai;

public sealed record ReadinessAiRequest(string? Goal, List<string> SessionIds);
