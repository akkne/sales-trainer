namespace Sellevate.Ai.Features.Dialog.Models;

public sealed class FeedbackResult
{
    public string Summary { get; set; } = null!;
    public string Content { get; set; } = null!;
    public int XpReward { get; set; }

    /// <summary>Overall performance grade from 0 to 10 shown to the user in feedback.</summary>
    public int Score { get; set; }
}
