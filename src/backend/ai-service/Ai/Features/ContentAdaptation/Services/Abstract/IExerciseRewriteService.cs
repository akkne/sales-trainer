using Sellevate.Ai.Features.ContentAdaptation.Models;

namespace Sellevate.Ai.Features.ContentAdaptation.Services.Abstract;

/// <summary>Phase 40.32. «Перепиши это упражнение под наш продукт и тон» — for exactly one exercise.</summary>
public interface IExerciseRewriteService
{
    Task<RewrittenExerciseDto> RewriteAsync(
        AdaptExerciseRequestDto request,
        CancellationToken cancellationToken = default);
}
