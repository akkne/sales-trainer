using Sellevate.Learning.Features.SkillTree.Models;

namespace Sellevate.Learning.Features.Lessons.Models;

public sealed class Lesson
{
    public Guid Id { get; set; }
    public Guid TopicId { get; set; }
    public int OrderInTopic { get; set; }
    public string Title { get; set; } = "";

    public Topic? Topic { get; set; }
}
