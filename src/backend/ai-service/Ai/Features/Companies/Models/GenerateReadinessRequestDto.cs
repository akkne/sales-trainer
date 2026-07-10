namespace Sellevate.Ai.Features.Companies.Models;

public sealed record GenerateReadinessRequestDto(string? Goal, List<string> SessionIds);
