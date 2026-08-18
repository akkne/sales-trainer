namespace Sellevate.Social.Features.Discuss.Constants;

/// <summary>
/// The sizes and weights the Discuss feed is tuned with. They live here rather than at their call
/// sites because the paging numbers are shared between a controller's query default and the
/// service's clamp — a default of 20 in the controller and a clamp that fell back to 50 would give
/// two different first pages for the same request — and because the hot-score weights are only
/// meaningful next to each other.
///
/// <para>
/// The score is <c>(upvotes * 4 + replies * 2 + log10(views + 1)) / (hoursSinceActivity + 2) ^ 0.5</c>,
/// plus a boost that keeps an admin-flagged thread above the fold: upvotes count double a reply,
/// views barely count at all, and the sub-linear decay means a busy thread stays visible for days
/// rather than hours. Scored in memory over the newest <see cref="HotCandidateCap"/> threads, so it
/// is an ordering of recent activity and not of the whole forum.
/// </para>
/// </summary>
internal static class DiscussFeedConstants
{
    /// <summary>Page size used when a caller asks for none, or asks for one out of range.</summary>
    public const int DefaultPageSize = 20;

    /// <summary>Largest page a caller may ask for; a bigger request silently becomes the default.</summary>
    public const int MaximumPageSize = 100;

    /// <summary>Threads pulled into memory for hot scoring before paging is applied.</summary>
    public const int HotCandidateCap = 1000;

    /// <summary>Popular-tag count used when a caller asks for none or a non-positive one.</summary>
    public const int DefaultPopularTagLimit = 10;

    /// <summary>How many authors the forum statistics panel names.</summary>
    public const int TopAuthorsCount = 3;

    /// <summary>How far back the top-author vote tally reaches.</summary>
    public const int TopAuthorsWindowDays = 7;

    /// <summary>Characters of a thread or reply body kept for a list preview.</summary>
    public const int BodyPreviewLength = 240;

    /// <summary>Appended to a body that was cut at <see cref="BodyPreviewLength"/>.</summary>
    public const string PreviewEllipsis = "…";

    public const int UpvoteScoreWeight = 4;
    public const int ReplyScoreWeight = 2;

    /// <summary>Added to the age in hours before decay, so a brand-new thread has a finite score.</summary>
    public const double DecayHoursOffset = 2;

    /// <summary>Sub-linear on purpose — see the class remarks.</summary>
    public const double DecayExponent = 0.5;

    /// <summary>Large enough that an admin-flagged thread outranks any organic score.</summary>
    public const double HotFlagScoreBoost = 1000;
}
