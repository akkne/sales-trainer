namespace Sellevate.Company.Features.Companies.FollowUpReminders;

/// <summary>
/// Configuration for the follow-up reminder background poll, bound from the
/// <c>FollowUpReminder</c> config section.
///
/// <para>
/// <see cref="BatchSize"/> is also this feature's blast radius, not only its throughput: the whole
/// claimed batch is stamped as notified before any of it is published, so a crash between the two
/// silently drops up to that many reminders for the tick. Raising it trades a safer worst case for
/// fewer round trips.
/// </para>
/// </summary>
public sealed class FollowUpReminderOptions
{
    public const string SectionName = "FollowUpReminder";

    /// <summary>How often the background service polls for due follow-ups. Default 5 minutes.</summary>
    public int PollIntervalMinutes { get; set; } = 5;

    /// <summary>Maximum number of due companies claimed (and published) per poll tick.</summary>
    public int BatchSize { get; set; } = 100;

    /// <summary>
    /// How many times one company's reminder is published before the tick gives up on it. Exists to
    /// absorb a transient broker blip (a leader election mid-tick), not to provide delivery
    /// guarantees — the company is already claimed by the time publishing starts, so exhausting these
    /// attempts drops that reminder rather than deferring it.
    /// </summary>
    public int PublishMaxAttempts { get; set; } = 3;

    /// <summary>
    /// Base of the linear backoff between publish attempts; attempt <c>n</c> waits <c>n</c> times
    /// this. Small on purpose: the whole retry window has to stay far inside one poll interval.
    /// </summary>
    public int PublishRetryBaseDelayMilliseconds { get; set; } = 100;
}
