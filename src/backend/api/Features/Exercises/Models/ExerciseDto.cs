using System.Text.Json;

namespace SalesTrainer.Api.Features.Exercises.Models;

public record ExerciseDto(
    Guid ExerciseId,
    string Type,
    int OrderInLesson,
    JsonElement Content
);
