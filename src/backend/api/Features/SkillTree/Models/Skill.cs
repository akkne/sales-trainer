namespace SalesTrainer.Api.Features.SkillTree.Models;

public class Skill
{
    public Guid Id { get; set; }
    public int OrderInTree { get; set; }
    public string Title { get; set; } = "";
    public string? Description { get; set; }
}
