using System.Text.Json.Nodes;

namespace SalesTrainer.Api.Features.Techniques.Models;

public sealed record TechniqueCaseDto(
    int OrderIndex,
    string Title,
    string Body,
    JsonObject? Metrics
);
