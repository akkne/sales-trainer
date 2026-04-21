namespace SalesTrainer.Api.Features.Techniques.Models;

public sealed record TechniqueDetailDto(
    TechniqueCardDto Card,
    string Body,
    string[] SkillIconicNames,
    TechniqueDialogTurnDto[] DialogTurns,
    TechniqueCaseDto? Case,
    TechniqueCoachDto? Coach
);
