namespace SalesTrainer.Api.Features.SkillTree.Models;

public sealed class UserSkillProgress
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid SkillId { get; set; }
    public string Status { get; set; } = "locked";
    public int CompletedLessonCount { get; set; }
    public int TotalLessonCount { get; set; }
}
