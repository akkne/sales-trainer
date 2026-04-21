namespace SalesTrainer.Api.Features.Techniques.Models;

public sealed record TechniqueMetaDto(
    TechniqueCategoryDto[] Categories,
    int TotalCount,
    TechniqueUserCountsDto UserCounts
);

public sealed record TechniqueUserCountsDto(
    int Mastered,
    int Master,
    int Unseen
);
