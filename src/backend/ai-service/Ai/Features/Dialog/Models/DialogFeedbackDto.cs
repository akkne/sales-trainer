namespace Sellevate.Ai.Features.Dialog.Models;

public sealed class DialogFeedbackDto
{
    public string Summary { get; set; } = null!;
    public string Content { get; set; } = null!;
    public int Score { get; set; }
    public DateTime GeneratedAt { get; set; }

    public static DialogFeedbackDto FromEntity(DialogFeedback feedback) => new()
    {
        Summary = feedback.Summary,
        Content = feedback.Content,
        Score = feedback.Score,
        GeneratedAt = feedback.GeneratedAt
    };
}
