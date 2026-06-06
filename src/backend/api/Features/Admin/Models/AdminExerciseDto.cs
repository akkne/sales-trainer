using System.Text.Json;

namespace SalesTrainer.Api.Features.Admin;

public record AdminExerciseDto(
    Guid Id,
    Guid LessonId,
    string Type,
    int OrderInLesson,
    JsonElement Content,
    string? CustomAiPrompt
);
