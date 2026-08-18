namespace Sellevate.Learning.Features.ContentAdaptation.Services.Abstract;

/// <summary>
/// Phase 40.32. The worker half of batch adaptation, run inside a scope whose organization the
/// caller has already set.
/// </summary>
public interface IContentAdaptationStepRunner
{
    /// <summary>Answers up to one tick's worth of items and returns how many were answered.</summary>
    Task<int> RunPendingAsync(CancellationToken cancellationToken = default);
}
