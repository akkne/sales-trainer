namespace Sellevate.Learning.Features.Techniques.Models;

public sealed record TechniqueCardDto(
    Guid Id,
    string Slug,
    string Name,
    string Summary,
    string[] Tags,
    string? PrimarySkillIconicName,
    string? PrimarySkillTitle,
    int Difficulty,
    string DifficultyName,
    int SortOrder,
    int MasteryLevel,
    int MasteryPercent,
    bool HasDialog,
    bool HasCase,
    bool HasCoach,
    bool IsNew
);
