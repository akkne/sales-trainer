namespace Sellevate.Learning.Features.ContentAdaptation.Models;

/// <summary>
/// Phase 40.32. One exercise as the scope query returns it — enough to write an item row and to
/// fingerprint the body the model will be shown, and nothing more.
/// </summary>
public sealed record ContentAdaptationScopeRow(
    Guid ExerciseId,
    Guid LessonId,
    string LessonTitle,
    string ExerciseType,
    int OrderInLesson,
    string SerializedContent,
    string? CustomAiPrompt);
