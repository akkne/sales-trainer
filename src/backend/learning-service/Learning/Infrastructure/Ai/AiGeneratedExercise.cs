using System.Text.Json;

namespace Sellevate.Learning.Infrastructure.Ai;

/// <summary>
/// Phase 40.27. One generated exercise, type plus opaque content — the seeder's bundle shape
/// (docs/SEEDER.md §3). It is validated by <c>ExerciseContentValidator</c> before it becomes a row,
/// and dropped rather than repaired if it does not pass.
/// </summary>
public sealed record AiGeneratedExercise(string Type, JsonElement Content);
