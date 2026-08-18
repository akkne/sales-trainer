namespace Sellevate.Learning.Features.ContentAdaptation.Models;

/// <summary>
/// Phase 40.32. A review finding exactly as it sits in the <c>jsonb</c> column: the code, and the
/// fragment of the exercise the model was pointing at.
///
/// <para>
/// The sentence and the severity are deliberately <b>not</b> here. They are looked up from
/// <c>ContentReviewFindingCodes</c> on every read, so that a stored document can never disagree with
/// the code table about what counts as blocking, and so that improving the wording of a complaint
/// improves it for every report ever written.
/// </para>
/// </summary>
public sealed record StoredContentReviewFinding(string Code, string? Detail);
