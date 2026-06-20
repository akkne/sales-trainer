namespace Sellevate.Gamification.Features.Gamification.Models;

public sealed class UserExperiencePointsRecord
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public int Amount { get; set; }
    public string Source { get; set; } = string.Empty;
    public DateTime EarnedAt { get; set; }
}
