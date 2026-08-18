namespace Sellevate.Learning.Features.ContentGeneration.Models;

/// <summary>
/// Phase 40.27. The run is not in a state where the requested transition means anything — editing
/// the structure of a run that is still structuring, approving one that has already produced a
/// lesson, retrying one that never failed.
///
/// <para>
/// Separate from <see cref="ContentGenerationValidationException"/> because it becomes a 409 rather
/// than a 400: the request was well-formed and the caller is not wrong about the world, they are
/// merely late. A screen polling a status needs to tell those two apart.
/// </para>
/// </summary>
public sealed class ContentGenerationStateException(string message) : Exception(message);
