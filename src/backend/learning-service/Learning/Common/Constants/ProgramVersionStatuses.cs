namespace Sellevate.Learning.Common.Constants;

/// <summary>
/// Phase 40.17. The lifecycle of a <c>ProgramVersion</c> row, stored as text so the partial unique
/// index, the check constraint and the freeze trigger in migration <c>AddProgramVersioning</c> can
/// all read it directly (docs/TENANCY/CONTENT_MODEL.md §2.5).
///
/// <para>
/// Deliberately a second vocabulary rather than a reuse of <see cref="LessonVersionStatuses"/>,
/// even though the three values are spelled the same today. A lesson version and a programme
/// version are frozen by different triggers for different reasons, and the day one of them grows a
/// fourth state — 40.18's <c>stale</c> is the obvious candidate for lessons — a shared constant
/// would silently widen the other one's check constraint too.
/// </para>
/// </summary>
public static class ProgramVersionStatuses
{
    /// <summary>The one mutable state. At most one draft may exist per organization at a time.</summary>
    public const string Draft = "draft";

    /// <summary>
    /// Frozen forever, structure included: neither the version's own row nor any of its
    /// <c>ProgramItems</c> may change again. That is the property the whole table exists for — a
    /// learner on lesson 8 of 21 is pinned to this row, and a snapshot that can be rearranged
    /// after the fact rearranges their programme underneath them.
    /// </summary>
    public const string Published = "published";

    /// <summary>
    /// Retired but kept, because enrollments still point at it. Written by nobody in 40.17 — the
    /// value exists so a later block does not have to widen the check constraint.
    /// </summary>
    public const string Archived = "archived";

    public static bool IsKnown(string status)
        => status is Draft or Published or Archived;
}
