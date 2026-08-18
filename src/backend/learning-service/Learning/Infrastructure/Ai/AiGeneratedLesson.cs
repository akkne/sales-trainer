namespace Sellevate.Learning.Infrastructure.Ai;

/// <summary>Phase 40.27. What came back from generation, before any of it has been validated or stored.</summary>
public sealed record AiGeneratedLesson(string Title, IReadOnlyList<AiGeneratedExercise> Exercises);
