namespace Sellevate.Company.Infrastructure.Ai;

public sealed record ReadinessAiRequest(Guid UserId, string? Goal, List<string> SessionIds);
