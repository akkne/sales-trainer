namespace Sellevate.Company.Infrastructure.Ai;

public sealed record ReadinessAiResult(
    int Score,
    List<string> Strengths,
    List<string> Gaps,
    string Recommendation);
