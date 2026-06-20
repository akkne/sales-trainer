namespace Sellevate.Gamification.Features.Gamification.Models;

public sealed class ExerciseTypeReward
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string ExerciseType { get; set; } = string.Empty;
    public int BaseXpReward { get; set; } = 10;
}
