namespace Sellevate.Learning.Common.Constants;

/// <summary>
/// Phase 40.15. The lifecycle of a <c>LessonVersion</c> row, stored as text so the partial unique
/// index and the freeze trigger in migration <c>AddLessonVersioning</c> can both read it directly
/// (docs/TENANCY/CONTENT_MODEL.md §2.1).
/// </summary>
public static class LessonVersionStatuses
{
    /// <summary>The one mutable state. At most one draft may exist per lesson at a time.</summary>
    public const string Draft = "draft";

    /// <summary>Frozen forever. Content, hash, version number and publication time never change again.</summary>
    public const string Published = "published";

    /// <summary>
    /// Retired but kept, because historical progress still points at it. Written by nobody in
    /// 40.15 — the value exists so 40.16/40.18 do not have to widen the check constraint.
    /// </summary>
    public const string Archived = "archived";

    public static bool IsKnown(string status)
        => status is Draft or Published or Archived;
}
