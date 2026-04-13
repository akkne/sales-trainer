namespace SalesTrainer.Api.Features.Lessons.Models;

public class Lesson
{
    public Guid Id { get; set; }
    public Guid SkillId { get; set; }
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public int SortOrder { get; set; }
    public int DifficultyLevel { get; set; }
    public int XpReward { get; set; }
    public int EstimatedMinutes { get; set; } = 5;
}
