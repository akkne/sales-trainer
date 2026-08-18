using Sellevate.Learning.Features.ContentGeneration.Models;

namespace Sellevate.Learning.Infrastructure.Ai;

/// <summary>
/// Phase 40.27. Generation: the confirmed structure, and <b>not the material</b>. That omission is
/// the block's whole economics — the deck is paid for once during structuring, and the reviewer's
/// deletions cannot be undone by a model re-reading the source they came from.
/// </summary>
public sealed record AiGenerateExercisesRequest(
    ContentStructureDto Structure,
    string? Focus,
    int MaximumExerciseCount);
