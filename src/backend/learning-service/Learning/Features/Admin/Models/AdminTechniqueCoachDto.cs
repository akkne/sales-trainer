using System.Text.Json.Nodes;

namespace Sellevate.Learning.Features.Admin;

public sealed record AdminTechniqueCoachDto(
    string AvatarSeed,
    string Name,
    string Role,
    string Quote,
    JsonNode? Challenges);
