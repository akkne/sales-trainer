using System.Text.Json;

namespace Sellevate.Ai.Features.ContentGeneration.Models;

/// <summary>
/// Phase 40.27. One generated exercise, in exactly the shape the seeder's bundle format already
/// uses (docs/SEEDER.md §3): a type and an opaque <c>content</c> document.
///
/// <para>
/// <b>The content stays a <see cref="JsonElement"/> all the way through this service.</b> Modelling
/// the eleven exercise-content schemas here would give the platform a second definition of them,
/// and the first time the two disagreed the disagreement would surface as a learner seeing a
/// question with no options. learning-service already owns the one definition —
/// <c>ExerciseContentValidator</c> — and it is the caller, so it validates what comes back and drops
/// what does not pass. An unvalidatable exercise is dropped, never repaired.
/// </para>
/// </summary>
public sealed record GeneratedExerciseDto(string Type, JsonElement Content);
