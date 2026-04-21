namespace SalesTrainer.Api.Features.Techniques.Models;

public sealed record TechniqueCardDto(
    Guid Id,
    string Slug,
    string Name,
    string Summary,
    string CategorySlug,
    string CategoryLabel,
    string CategoryColor,
    string[] Tags,
    string? PrimarySkillIconicName,
    int SortOrder,
    int Level,
    string LevelName,
    int MasteryPercent,
    bool IsNew
);
