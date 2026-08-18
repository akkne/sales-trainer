using Sellevate.Ai.Features.ContentAdaptation.Models;

namespace Sellevate.Ai.Features.ContentAdaptation.Services.Abstract;

/// <summary>
/// Phase 40.32. «Что не так с этим упражнением» — the half of the block that never changes anything.
/// </summary>
public interface IExerciseReviewService
{
    Task<ExerciseReviewDto> ReviewAsync(
        AdaptExerciseRequestDto request,
        CancellationToken cancellationToken = default);
}
