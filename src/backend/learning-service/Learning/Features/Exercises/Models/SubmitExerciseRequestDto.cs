using System.Text.Json;

namespace Sellevate.Learning.Features.Exercises.Models;

/// <summary>
/// <paramref name="Skipped"/> (X-4) records that the learner pressed "Skip" rather than answering:
/// the attempt is stored as a real, un-gradeable attempt (never correct, no AI call) so the
/// server's "every exercise attempted" completion gate advances the same way the client's progress
/// bar already does. Omitted by any existing caller, so it defaults to <c>false</c> and changes
/// nothing for a normal submission.
/// </summary>
public record SubmitExerciseRequestDto(JsonElement Answer, bool Skipped = false);
