namespace Sellevate.Analytics.Features.Tracking.Constants;

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
