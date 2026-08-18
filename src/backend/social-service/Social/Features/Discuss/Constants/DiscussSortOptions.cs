namespace Sellevate.Social.Features.Discuss.Constants;

/// <summary>
/// The <c>sort</c> query values the Discuss feed accepts, spelled the same way in the controller
/// default, in the admin controller's fixed query and in the service that branches on them. Anything
/// else falls through to <see cref="Hot"/>, so a typo here silently changes the default ordering
/// rather than failing — which is why the three call sites share one set of names.
/// </summary>
internal static class DiscussSortOptions
{
    /// <summary>Pinned first, then the time-decayed score. The default when nothing is asked for.</summary>
    public const string Hot = "hot";

    /// <summary>Most recent activity first.</summary>
    public const string New = "new";

    /// <summary>Threads with no replies yet, newest first.</summary>
    public const string Unanswered = "unanswered";
}
