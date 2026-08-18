namespace Sellevate.Learning.Features.ContentAdaptation.Models;

/// <summary>Phase 40.32. The caller asked for something malformed. The controller answers 400.</summary>
public sealed class ContentAdaptationValidationException(string message) : Exception(message);
