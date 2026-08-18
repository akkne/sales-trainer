namespace Sellevate.Ai.Features.Dialog.Models;

public sealed class ValidateScenarioResponseDto
{
    public bool IsValid { get; set; }

    public string? RejectionReason { get; set; }
}
