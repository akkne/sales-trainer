namespace SalesTrainer.Api.Features.SkillTree.Models;

public sealed class Skill
{
    public Guid Id { get; set; }
    public string IconicName { get; set; } = "";
    public int OrderInTree { get; set; }
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public string Stage { get; set; } = "general";
}
