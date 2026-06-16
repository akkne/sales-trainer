namespace SalesTrainer.Api.Features.SkillTree.Models;

/// <summary>
/// A configurable funnel stage used to group skills on the skill tree (e.g.
/// preparation, discovery). Replaces the previously frontend-hardcoded stage
/// list: <see cref="Key"/> is the stable slug stored on <see cref="Skill.Stage"/>,
/// while <see cref="Label"/>/<see cref="Accent"/> drive presentation and
/// <see cref="Order"/> defines the display order along the sales funnel
/// (ascending — lowest order shown first).
/// </summary>
public sealed class SkillStage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Accent { get; set; } = string.Empty;
    public int Order { get; set; }
}
