namespace Sellevate.Learning.Features.ContentGeneration.Models;

/// <summary>
/// Phase 40.27. A request the pipeline refuses — an empty structure, a state that cannot make the
/// transition being asked for. Same shape as <c>AssignmentValidationException</c>: the controller
/// turns it into a 400 or a 409 and nothing else in the service catches it.
/// </summary>
public sealed class ContentGenerationValidationException(string message) : Exception(message);
