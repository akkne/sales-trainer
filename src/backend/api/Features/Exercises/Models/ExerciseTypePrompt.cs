namespace SalesTrainer.Api.Features.Exercises.Models;

public sealed class ExerciseTypePrompt
{
    public Guid Id { get; set; }
    public string ExerciseType { get; set; } = "";
    public string SystemPrompt { get; set; } = "";
    public DateTime UpdatedAt { get; set; }
}
