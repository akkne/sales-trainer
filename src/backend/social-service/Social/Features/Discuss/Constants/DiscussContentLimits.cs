namespace Sellevate.Social.Features.Discuss.Constants;

/// <summary>
/// The maximum lengths of the Discuss text columns. Changing one of these needs a migration, so they
/// are an invariant of the stored schema rather than a knob: the value here is the value the column
/// was created with.
///
/// <para>
/// <see cref="TagSlugMaximumLength"/> is load-bearing in two places — the column width and the
/// truncation in the slug builder. They have to agree, or a long tag name produces a slug the
/// database rejects at the last moment.
/// </para>
/// </summary>
internal static class DiscussContentLimits
{
    public const int ThreadTitleMaximumLength = 300;

    /// <summary>Applies to a thread body and to a reply body alike.</summary>
    public const int BodyMaximumLength = 20000;

    public const int TagSlugMaximumLength = 60;
    public const int TagNameMaximumLength = 60;
}
