namespace Sellevate.Ai.Features.Dialog.Models;

/// <summary>Verdict of the sales-relevance check for a user-authored scenario.</summary>
public sealed record ScenarioValidationResult(bool IsValid, string? RejectionReason)
{
    public static ScenarioValidationResult Valid() => new(true, null);

    public static ScenarioValidationResult Invalid(string rejectionReason) => new(false, rejectionReason);
}
