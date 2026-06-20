using System.Text.Json;

namespace Sellevate.Learning.Infrastructure.Ai;

public sealed record AiEvaluationRequest(
    string ExerciseType,
    string? SystemPrompt,
    JsonElement ExerciseContent,
    JsonElement UserAnswer);
