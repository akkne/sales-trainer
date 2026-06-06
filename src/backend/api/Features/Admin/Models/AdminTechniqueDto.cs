using System.Text.Json.Nodes;

namespace SalesTrainer.Api.Features.Admin;

public sealed record AdminTechniqueDto(
    Guid Id,
    string Slug,
    string Name,
    string Summary,
    string Body,
    string[] Tags,
    Guid? PrimarySkillId,
    string? PrimarySkillIconicName,
    string? PrimarySkillTitle,
    Guid[] AdditionalSkillIds,
    int Difficulty,
    string DifficultyName,
    int SortOrder,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    JsonNode? Dialog,
    JsonNode? Case,
    AdminTechniqueCoachDto? Coach);
