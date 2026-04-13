namespace SalesTrainer.Api.Features.Achievements.Models;

public sealed class Achievement
{
    public Guid Id { get; set; }

    public string Key { get; set; } = "";

    public string Title { get; set; } = "";
    public string Description { get; set; } = "";

    public string IconEmoji { get; set; } = "";

    public string ConditionType { get; set; } = "";

    public int ConditionThreshold { get; set; }

    public int SortOrder { get; set; }
}
