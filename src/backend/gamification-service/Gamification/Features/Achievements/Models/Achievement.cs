namespace Sellevate.Gamification.Features.Achievements.Models;

public sealed class Achievement
{
    public Guid Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string IconEmoji { get; set; } = string.Empty;
    public string ConditionType { get; set; } = string.Empty;
    public int ConditionThreshold { get; set; }
    public int SortOrder { get; set; }
}
