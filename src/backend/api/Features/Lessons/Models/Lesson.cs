using SalesTrainer.Api.Features.SkillTree.Models;

namespace SalesTrainer.Api.Features.Lessons.Models;

public class Lesson
{
    public Guid Id { get; set; }
    public Guid TopicId { get; set; }
    public int OrderInTopic { get; set; }
    public string Title { get; set; } = "";

    public Topic? Topic { get; set; }
}
