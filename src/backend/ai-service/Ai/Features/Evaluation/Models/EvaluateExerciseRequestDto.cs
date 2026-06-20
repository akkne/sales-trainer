using System.Text.Json;

namespace Sellevate.Ai.Features.Evaluation.Models;

public sealed record EvaluateExerciseRequestDto(
    string ExerciseType,
    string? SystemPrompt,
    JsonElement ExerciseContent,
    JsonElement UserAnswer);
