using System.Text.Json;

namespace Sellevate.Learning.Features.Exercises.Models;

public record ExerciseDto(
    Guid ExerciseId,
    string Type,
    int OrderInLesson,
    JsonElement Content
);
