namespace SalesTrainer.Api.Features.Lessons;

public class UserExerciseAttempt
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid ExerciseId { get; set; }
    public string SerializedAnswer { get; set; } = "{}";
    public bool IsCorrect { get; set; }
    public int Score { get; set; }
    public string? SerializedAiFeedback { get; set; }
    public DateTime AttemptedAt { get; set; }
}
