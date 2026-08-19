namespace Sellevate.Analytics.Features.Tracking.Constants;

/// <summary>
/// The server-side whitelist of page and event names the frontend is allowed to report.
///
/// <para>
/// <b>This is a cardinality cap, not a validation list.</b> Both sets become Prometheus label values
/// on <c>app_page_views_total</c> and <c>app_events_total</c>, so every entry added here costs a
/// permanent time series and every entry <i>not</i> here costs nothing but a 400. A unit test asserts
/// both sets stay under the cap; before adding a name, read the cardinality rules in
/// docs/MONITORING.md and prefer reusing an existing one.
/// </para>
/// </summary>
public static class TrackedEvents
{
    public const string PageViewEvent = "page_view";

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
        // Missing since 40.7 renamed the frontend's /register page to /invite without following it
        // here, so every invite-acceptance page view was answered with a 400 instead of counted.
        "invite",
        "onboarding",
        "admin",
        "other",
    };

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

    public static bool IsKnownEvent(string? trackedEvent) =>
        trackedEvent is not null && Events.Contains(trackedEvent);
}
