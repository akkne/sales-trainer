namespace SalesTrainer.Api.Features.SkillTree.Models;

public class Skill
{
    public Guid Id { get; set; }
    public string Slug { get; set; } = "";
    public string Title { get; set; } = "";
    public string IconName { get; set; } = "";
    public int SortOrder { get; set; }
    public Guid? PrerequisiteSkillId { get; set; }
    public string[] ApplicableSalesTypes { get; set; } = [];
}
