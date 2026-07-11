namespace Sellevate.Ai.Features.Companies.Models;

public sealed record ReadinessResultDto(
    int Score,
    List<string> Strengths,
    List<string> Gaps,
    string Recommendation);
