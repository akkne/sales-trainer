using System.Text.Json;

namespace Sellevate.Learning.Features.Admin;

public record CreateExerciseRequestDto(
    string Type,
    int OrderInLesson,
    JsonElement Content,
    string? CustomAiPrompt
);
