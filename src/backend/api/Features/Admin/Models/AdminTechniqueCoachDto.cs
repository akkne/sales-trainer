using System.Text.Json.Nodes;

namespace SalesTrainer.Api.Features.Admin;

public sealed record AdminTechniqueCoachDto(
    string AvatarSeed,
    string Name,
    string Role,
    string Quote,
    JsonNode? Challenges);
