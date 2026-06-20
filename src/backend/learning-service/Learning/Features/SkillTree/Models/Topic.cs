namespace Sellevate.Learning.Features.SkillTree.Models;

public sealed class Topic
{
    public Guid Id { get; set; }
    public Guid SkillId { get; set; }
    public string IconicName { get; set; } = "";
    public int OrderInSkill { get; set; }
    public string Title { get; set; } = "";

    public Skill? Skill { get; set; }
}
