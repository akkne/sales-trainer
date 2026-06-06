using System.Text.Json;

namespace SalesTrainer.Api.Features.Admin;

public record CreateExerciseRequestDto(
    string Type,
    int OrderInLesson,
    JsonElement Content,
    string? CustomAiPrompt
);
