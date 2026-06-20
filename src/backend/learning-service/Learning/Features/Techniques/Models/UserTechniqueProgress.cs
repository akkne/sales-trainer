namespace Sellevate.Learning.Features.Techniques.Models;

public sealed class UserTechniqueProgress
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid TechniqueId { get; set; }
    public int Level { get; set; }
    public int MasteryPercent { get; set; }
    public int PracticeCount { get; set; }
    public DateTime? LastPracticedAt { get; set; }
    public DateTime FirstSeenAt { get; set; } = DateTime.UtcNow;
}
