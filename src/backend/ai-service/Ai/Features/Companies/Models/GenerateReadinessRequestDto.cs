namespace Sellevate.Ai.Features.Companies.Models;

public sealed record GenerateReadinessRequestDto(Guid UserId, string? Goal, List<string> SessionIds);
