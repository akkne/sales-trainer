namespace Sellevate.Learning.Features.ContentAdaptation.Models;

/// <summary>
/// Phase 40.32. One field that moved between the current exercise body and the proposed one — the
/// machine-readable half of «дифф».
///
/// <para>
/// <b>This is an enumeration, not a merge.</b> 40.18 refused to build a three-way merge of prose and
/// grading criteria on the grounds that it produces plausible-looking nonsense which then grades a
/// salesperson, and that refusal stands here: nothing on the server combines the two documents. What
/// it does do is say which leaves differ, which is a deterministic comparison of two trees and lets
/// the screen show «изменено: options[1].text» instead of two walls of JSON. Both documents travel
/// alongside it, so the screen can always fall back to showing them whole.
/// </para>
/// </summary>
/// <param name="Path">Dotted leaf path inside the exercise body, e.g. <c>options[1].text</c>.</param>
/// <param name="Before">The current value, truncated for display. Null when the field is new.</param>
/// <param name="After">The proposed value, truncated for display. Null when the field was removed.</param>
public sealed record ContentFieldChangeDto(
    string Path,
    string? Before,
    string? After);
