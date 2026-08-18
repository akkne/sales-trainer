using System.Globalization;

namespace Sellevate.Learning.Common.Constants;

/// <summary>
/// Phase 40.31. The <c>&lt;kind&gt;:&lt;id&gt;</c> namespace an assignment uses when its source is a
/// measurement rather than a document (docs/TENANCY/ASSIGNMENTS.md §3.4).
///
/// <para>
/// <b>A metric is not a row, so the reference has to carry its own coordinates.</b> 40.21 fixed the
/// shape of <c>Assignment.SourceRef</c> as a namespaced string and gave <c>gap_detected</c> the
/// sentence «SourceRef names the metric that triggered it» without saying what such a string looks
/// like. It looks like <c>skill-gap:closing@2026-08-18</c>: the funnel stage the team failed at, and
/// the day the failure was observed. Those two facts are exactly what it takes to reconstruct the
/// number a year later — the heat map of <c>GET /admin/team/skill-map</c> for that stage, read over
/// the window that ended on that date.
/// </para>
///
/// <para>
/// <b>The stage half is the identity and the date half is the evidence.</b> Everything that has to
/// recognise "this gap again" — a dismissal, an in-flight generation run — keys on the stage alone,
/// because a second observation of the same weak stage a week later is the same gap and not a new
/// one. The date exists so that a row written months ago still says which observation paid for it.
/// </para>
/// </summary>
public static class SkillGapSourceRefs
{
    /// <summary>The namespace. Everything after it is the stage key and the observation date.</summary>
    public const string Kind = "skill-gap";

    /// <summary>
    /// The longest stage key this vocabulary will build a reference from. <c>Skill.Stage</c> is a
    /// short lookup key (<c>closing</c>, <c>discovery</c>), and refusing a long one here keeps the
    /// whole reference inside the 200 characters <c>Assignments.SourceRef</c> allows.
    /// </summary>
    public const int MaximumStageKeyLength = 64;

    private const string DateFormat = "yyyy-MM-dd";

    /// <summary>
    /// <c>skill-gap:&lt;stageKey&gt;@&lt;yyyy-MM-dd&gt;</c>. The date is the day the gap was observed,
    /// in UTC, because every window in the dashboard is computed from <c>DateTime.UtcNow</c>.
    /// </summary>
    public static string Build(string stageKey, DateTime observedAt)
        => $"{Kind}:{stageKey}@{observedAt.ToString(DateFormat, CultureInfo.InvariantCulture)}";

    /// <summary>
    /// The prefix every reference to one stage's gap starts with, whenever it was observed. This is
    /// what "is there already a run working on this gap" asks, and it is why the stage comes before
    /// the date rather than after it.
    /// </summary>
    public static string BuildStagePrefix(string stageKey) => $"{Kind}:{stageKey}@";

    /// <summary>
    /// Whether a stage key can appear inside a reference at all. The two separators are refused
    /// because a key containing one would make the reference ambiguous to split, and an empty or
    /// over-long key would make it unreadable.
    /// </summary>
    public static bool IsUsableStageKey(string? stageKey)
        => !string.IsNullOrWhiteSpace(stageKey)
           && stageKey.Length <= MaximumStageKeyLength
           && stageKey.Trim().Length == stageKey.Length
           && !stageKey.Contains(':', StringComparison.Ordinal)
           && !stageKey.Contains('@', StringComparison.Ordinal);

    /// <summary>Whether the string is in this namespace at all.</summary>
    public static bool IsSkillGapReference(string? sourceRef)
        => sourceRef is not null && sourceRef.StartsWith($"{Kind}:", StringComparison.Ordinal);

    /// <summary>
    /// The stage a reference names, or <see langword="null"/> when the string is not one of ours.
    /// Tolerant on the way out for the reason <c>AssignmentDocumentSerializer</c> is: a row written
    /// by another version of this service must degrade to "unknown gap" rather than throw out of a
    /// list endpoint.
    /// </summary>
    public static string? TryReadStageKey(string? sourceRef)
    {
        if (!IsSkillGapReference(sourceRef))
        {
            return null;
        }

        var body = sourceRef![(Kind.Length + 1)..];
        var separatorIndex = body.IndexOf('@', StringComparison.Ordinal);
        var stageKey = separatorIndex < 0 ? body : body[..separatorIndex];

        return stageKey.Length == 0 ? null : stageKey;
    }
}
