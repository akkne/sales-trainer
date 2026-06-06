namespace SalesTrainer.Api.Features.Techniques.Models;

public sealed record TechniqueMetaDto(
    TechniqueSkillFacetDto[] Skills,
    int TotalCount,
    TechniqueUserCountsDto UserCounts
);
