using Sellevate.Ai.Features.Dialog.Models;

namespace Sellevate.Ai.Features.Dialog.Services.Abstract;

/// <summary>
/// Decides whether a user-authored scenario is actually about sales before it is allowed to become
/// the system prompt of a practice conversation.
/// </summary>
public interface IScenarioValidationService
{
    /// <summary>
    /// Returns the sales-relevance verdict for <paramref name="scenario"/>, answering from cache
    /// when the same text has been judged before.
    /// </summary>
    /// <exception cref="ScenarioValidationUnavailableException">
    /// The check could not be performed. Callers must fail closed — never treat this as approval.
    /// </exception>
    Task<ScenarioValidationResult> ValidateAsync(string scenario, CancellationToken cancellationToken = default);
}
