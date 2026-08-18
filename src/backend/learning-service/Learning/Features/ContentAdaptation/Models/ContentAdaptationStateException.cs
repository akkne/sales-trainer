namespace Sellevate.Learning.Features.ContentAdaptation.Models;

/// <summary>
/// Phase 40.32. The request was well-formed and the world disagrees: an already-answered item, a
/// proposal computed against an exercise somebody has since edited, an accept aimed at a review
/// finding there is nothing to apply. The controller answers 409, never 400 — the caller was not
/// wrong, the answer is simply no.
/// </summary>
public sealed class ContentAdaptationStateException(string message) : Exception(message);
