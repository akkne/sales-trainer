namespace Sellevate.Ai.Features.ContentGeneration.Models;

/// <summary>
/// Phase 40.27. The second half of the pipeline, and the reason the checkpoint pays for itself
/// twice.
///
/// <para>
/// <b>The material is not in this request, on purpose.</b> Generation reads the confirmed structure
/// and nothing else. That is the token saving the roadmap names — a fifty-page deck is paid for once,
/// during structuring, instead of again on every generation and re-generation — and it is also what
/// makes the human's edit binding: if the raw material travelled alongside the structure, the model
/// would keep finding the objection the РОП deleted and putting it back.
/// </para>
/// </summary>
/// <param name="Structure">The structure as the human left it, not as the model first read it.</param>
/// <param name="Focus">
/// What the training is about, in the РОП's own words — the assignment title, usually. Steers which
/// slice of the structure the exercises exercise. Optional.
/// </param>
/// <param name="MaximumExerciseCount">
/// An upper bound, never a target. «Лучше 4 хороших упражнения, чем 15 ватных» is 40.28's sentence,
/// but the bound belongs here already: a model asked for exactly fifteen pads.
/// </param>
public sealed record GenerateExercisesRequestDto(
    ExtractedContentStructureDto Structure,
    string? Focus = null,
    int MaximumExerciseCount = 8);
