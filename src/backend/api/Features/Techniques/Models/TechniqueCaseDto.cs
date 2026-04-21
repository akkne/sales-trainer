using System.Text.Json.Nodes;

namespace SalesTrainer.Api.Features.Techniques.Models;

public sealed record TechniqueCaseDto(
    string Title,
    string Body,
    JsonObject? Metrics
);
