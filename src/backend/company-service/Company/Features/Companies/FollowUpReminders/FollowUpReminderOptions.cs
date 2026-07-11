namespace Sellevate.Company.Features.Companies.FollowUpReminders;

/// <summary>
/// Configuration for the follow-up reminder background poll, bound from the
/// <c>FollowUpReminder</c> config section.
/// </summary>
public sealed class FollowUpReminderOptions
{
    public const string SectionName = "FollowUpReminder";

    /// <summary>How often the background service polls for due follow-ups. Default 5 minutes.</summary>
    public int PollIntervalMinutes { get; set; } = 5;

    /// <summary>Maximum number of due companies claimed (and published) per poll tick.</summary>
    public int BatchSize { get; set; } = 100;
}
