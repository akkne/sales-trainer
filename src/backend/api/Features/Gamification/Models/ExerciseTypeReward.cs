namespace SalesTrainer.Api.Features.Gamification.Models;

/// <summary>
/// Admin-editable base XP awarded per exercise type when a user answers correctly
/// (or passes an AI-evaluated exercise). One row per exercise type identifier.
/// </summary>
public sealed class ExerciseTypeReward
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Exercise type identifier (see <c>ExerciseTypes</c>), e.g. "choose_option".</summary>
    public string ExerciseType { get; set; } = "";

    /// <summary>XP granted on a correct/passed answer for this type.</summary>
    public int BaseXpReward { get; set; } = 10;
}
