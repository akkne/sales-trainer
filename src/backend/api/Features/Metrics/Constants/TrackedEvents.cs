namespace SalesTrainer.Api.Features.Metrics.Constants;

/// <summary>
/// Server-side whitelist of allowed metric label values. This is the hard cap on
/// label cardinality for <c>app_events_total</c> / <c>app_page_views_total</c>: a
/// buggy or hostile client cannot inflate the series count because unknown values are
/// rejected at the tracking endpoint. Keep each set small (~15 entries) and meaningful.
/// </summary>
public static class TrackedEvents
{
    /// <summary>Special event name the tracking endpoint treats as a page view.</summary>
    public const string PageViewEvent = "page_view";

    /// <summary>Bounded set of page names (mapped from frontend routes).</summary>
    public static readonly IReadOnlySet<string> Pages = new HashSet<string>
    {
        "tree",
        "league",
        "dialog",
        "profile",
        "guidebook",
        "friends",
        "discuss",
        "session",
        "login",
        "register",
        "onboarding",
        "admin",
        "other",
    };

    /// <summary>Bounded set of UI action/click events.</summary>
    public static readonly IReadOnlySet<string> Events = new HashSet<string>
    {
        PageViewEvent,
        "start_dialog",
        "start_lesson",
        "complete_lesson",
        "open_skill",
        "open_technique",
        "send_message",
        "add_friend",
        "open_league",
        "edit_profile",
    };

    public static bool IsKnownPage(string? page) =>
        page is not null && Pages.Contains(page);

    public static bool IsKnownEvent(string? @event) =>
        @event is not null && Events.Contains(@event);
}
