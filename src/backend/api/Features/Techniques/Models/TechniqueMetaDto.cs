namespace SalesTrainer.Api.Features.Techniques.Models;

public sealed record TechniqueMetaDto(
    TechniqueSkillFacetDto[] Skills,
    int TotalCount,
    TechniqueUserCountsDto UserCounts
);

public sealed record TechniqueSkillFacetDto(
    string IconicName,
    string Title,
    int TechniqueCount
);

public sealed record TechniqueUserCountsDto(
    int Mastered,
    int Master,
    int Unseen
);
