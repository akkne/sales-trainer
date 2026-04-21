namespace SalesTrainer.Api.Features.Techniques.Models;

public sealed record TechniqueCoachDto(
    string AvatarSeed,
    string Name,
    string Role,
    string Quote,
    TechniqueCoachChallengeDto[] Challenges
);

public sealed record TechniqueCoachChallengeDto(
    string Label,
    string? Kind,
    string? TargetSlug
);
