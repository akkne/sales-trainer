using System.Text.Json.Nodes;

namespace Sellevate.Learning.Features.Techniques.Models;

public sealed record TechniqueCaseDto(
    string Title,
    string Body,
    JsonObject? Metrics
);
