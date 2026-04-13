namespace SalesTrainer.Api.Features.Dialog;

public sealed class FeedbackResult
{
    public string Summary { get; set; } = null!;
    public string Content { get; set; } = null!;
    public int XpReward { get; set; }
}
