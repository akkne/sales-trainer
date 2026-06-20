using System.Text.Json;

namespace Sellevate.Learning.Features.Exercises.Models;

public record SubmitExerciseRequestDto(JsonElement Answer);
