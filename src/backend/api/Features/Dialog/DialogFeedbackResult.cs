namespace SalesTrainer.Api.Features.Dialog;

public sealed class DialogFeedbackResult
{
    public DialogFeedback Feedback { get; set; } = null!;
    public int XpEarned { get; set; }
}
