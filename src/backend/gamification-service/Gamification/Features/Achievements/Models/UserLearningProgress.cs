namespace Sellevate.Gamification.Features.Achievements.Models;

public sealed class UserLearningProgress
{
    public Guid UserId { get; set; }
    public int CompletedLessonCount { get; set; }
    public bool HasCompletedAnySkill { get; set; }
    public DateTime UpdatedAt { get; set; }
}
