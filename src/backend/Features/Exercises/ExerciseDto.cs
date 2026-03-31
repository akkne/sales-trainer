using System.Text.Json;

namespace SalesTrainer.Api.Features.Exercises;

public record ExerciseDto(
    Guid ExerciseId,
    string Type,
    int SortOrder,
    JsonElement Content
);
