namespace Sellevate.Ai.Features.Dialog.Models;

/// <summary>Thrown when a scenario reached session start without being about sales.</summary>
public sealed class ScenarioRejectedException : Exception
{
    public ScenarioRejectedException(string message) : base(message)
    {
    }
}
