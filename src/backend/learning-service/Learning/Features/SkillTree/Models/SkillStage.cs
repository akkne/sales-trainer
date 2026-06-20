namespace Sellevate.Learning.Features.SkillTree.Models;

public sealed class SkillStage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Accent { get; set; } = string.Empty;
    public int Order { get; set; }
}
