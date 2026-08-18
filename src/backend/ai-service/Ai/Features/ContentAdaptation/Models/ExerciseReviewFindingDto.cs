namespace Sellevate.Ai.Features.ContentAdaptation.Models;

/// <summary>
/// Phase 40.32. One defect the reviewer found: a code from <see cref="ContentReviewCodes"/> and, at
/// most, the fragment of the exercise it is pointing at.
/// </summary>
/// <param name="Code">A code from the closed list. Anything else is dropped by the caller.</param>
/// <param name="Detail">
/// The offending fragment in the exercise's own words — the option text, the criterion. Quoting
/// rather than explaining, because the explanation is a fixed sentence the caller already has and the
/// one thing it cannot know is <i>which</i> option is the problem.
/// </param>
public sealed record ExerciseReviewFindingDto(string Code, string? Detail);
