using System.Text.Json;

namespace SalesTrainer.Api.Features.Exercises.Models;

public record SubmitExerciseRequestDto(JsonElement Answer);
