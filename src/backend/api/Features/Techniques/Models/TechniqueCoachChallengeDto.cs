namespace SalesTrainer.Api.Features.Techniques.Models;

public sealed record TechniqueCoachChallengeDto(
    string Label,
    string? Kind,
    string? TargetSlug
);
