namespace Sellevate.Learning.Infrastructure.Ai;

/// <summary>
/// Phase 40.32. One finding on the wire: a code from the closed vocabulary and, at most, the quoted
/// fragment it points at. The Russian sentence and the blocking/advisory split are resolved from
/// <c>ContentReviewFindingCodes</c> on this side — ai-service never writes prose a customer reads.
/// </summary>
public sealed record AiExerciseReviewFinding(string Code, string? Detail);
