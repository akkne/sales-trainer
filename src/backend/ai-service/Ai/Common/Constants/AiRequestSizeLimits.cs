namespace Sellevate.Ai.Common.Constants;

/// <summary>
/// Caps on what an internal caller may push into a prompt, refused at the controller before any
/// provider is paid.
///
/// <para>
/// These are guard rails rather than tuning: the numbers exist because an unbounded body becomes an
/// unbounded token bill and a prompt the model silently truncates in the middle, so raising one is a
/// change to what the service promises rather than a knob an operator turns. Stated here because the
/// same two values were repeated across five controllers and one service, which is how two routes
/// accepting "the same" text end up disagreeing about how much of it they accept.
/// </para>
/// </summary>
public static class AiRequestSizeLimits
{
    /// <summary>
    /// Characters of caller-supplied prose a single request may carry: a graded answer, a company
    /// description, a raw call log.
    /// </summary>
    public const int MaximumPromptTextCharacters = 16_000;

    /// <summary>
    /// Characters of source material a content request may carry. Larger than
    /// <see cref="MaximumPromptTextCharacters"/> because the caller is uploading a document rather
    /// than typing an answer; content generation truncates at this length rather than refusing.
    /// </summary>
    public const int MaximumSourceMaterialCharacters = 60_000;

    /// <summary>Dialog sessions one readiness request may ask about.</summary>
    public const int MaximumSessionIdsPerRequest = 50;
}
