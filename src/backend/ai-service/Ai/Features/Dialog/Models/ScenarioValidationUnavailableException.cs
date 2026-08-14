namespace Sellevate.Ai.Features.Dialog.Models;

/// <summary>
/// Thrown when the relevance check could not produce a verdict at all — the provider failed, or
/// answered with something unparseable.
/// </summary>
/// <remarks>
/// Deliberately distinct from a rejection. A rejection is a real answer about the user's text and
/// gets cached; this is "we don't know", which must never be cached and must never be treated as
/// approval — an unavailable checker fails closed.
/// </remarks>
public sealed class ScenarioValidationUnavailableException : Exception
{
    public ScenarioValidationUnavailableException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
