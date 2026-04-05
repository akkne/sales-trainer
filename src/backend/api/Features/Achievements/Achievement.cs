namespace SalesTrainer.Api.Features.Achievements;

public class Achievement
{
    public Guid Id { get; set; }

    /// <summary>Machine-readable key, e.g. "first_lesson", "streak_7".</summary>
    public string Key { get; set; } = "";

    public string Title { get; set; } = "";
    public string Description { get; set; } = "";

    /// <summary>Emoji or icon name displayed in the badge UI.</summary>
    public string IconEmoji { get; set; } = "";

    /// <summary>Condition type: "first_lesson" | "lesson_count" | "xp_total" | "streak_days" | "skill_completed".</summary>
    public string ConditionType { get; set; } = "";

    /// <summary>Numeric threshold for count/xp/streak conditions. 0 for event-based conditions.</summary>
    public int ConditionThreshold { get; set; }

    public int SortOrder { get; set; }
}
