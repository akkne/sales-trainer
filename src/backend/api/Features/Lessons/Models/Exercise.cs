namespace SalesTrainer.Api.Features.Lessons.Models;

public sealed class Exercise
{
    public Guid Id { get; set; }
    public Guid LessonId { get; set; }
    public string Type { get; set; } = "";
    public int OrderInLesson { get; set; }
    public string SerializedContent { get; set; } = "{}";
    public string? CustomAiPrompt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Lesson? Lesson { get; set; }
}
