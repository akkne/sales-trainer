namespace Sellevate.Ai.Features.Dialog.Models;

public sealed class DialogFeedbackResult
{
    public DialogFeedback Feedback { get; set; } = null!;
    public int XpEarned { get; set; }
}
