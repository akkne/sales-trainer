using System.Text.Json.Nodes;

namespace Sellevate.Learning.Features.Admin;

public sealed record AdminTechniqueWriteRequestDto(
    string Slug,
    string Name,
    string Summary,
    string Body,
    string[]? Tags,
    Guid? PrimarySkillId,
    Guid[]? AdditionalSkillIds,
    int Difficulty,
    int SortOrder,
    JsonNode? Dialog,
    JsonNode? Case,
    AdminTechniqueCoachDto? Coach);
