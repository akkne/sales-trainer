using System.Text.Json;

namespace SalesTrainer.Api.Features.Exercises;

public record SubmitExerciseRequestDto(JsonElement Answer);
