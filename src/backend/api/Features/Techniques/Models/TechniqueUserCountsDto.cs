namespace SalesTrainer.Api.Features.Techniques.Models;

public sealed record TechniqueUserCountsDto(
    int Mastered,
    int Master,
    int Unseen
);
