namespace Sellevate.Learning.Infrastructure.Ai;

public interface IAiEvaluationClient
{
    Task<AiEvaluationResult> EvaluateAsync(
        AiEvaluationRequest request,
        CancellationToken cancellationToken = default);
}
