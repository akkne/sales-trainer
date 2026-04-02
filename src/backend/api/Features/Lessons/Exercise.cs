namespace SalesTrainer.Api.Features.Lessons;

public class Exercise
{
    public Guid Id { get; set; }
    public Guid LessonId { get; set; }
    public string Type { get; set; } = "";
    public int SortOrder { get; set; }
    public string SerializedContent { get; set; } = "{}";
}
