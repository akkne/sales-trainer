namespace SalesTrainer.Api.Features.Lessons;

public class UserLessonProgress
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid LessonId { get; set; }
    public string Status { get; set; } = "locked";
    public int BestScore { get; set; }
    public DateTime? CompletedAt { get; set; }
}
