namespace Sellevate.Ai.Features.Dialog.Models;

public sealed class DialogFeedbackDto
{
    public string Summary { get; set; } = null!;
    public string Content { get; set; } = null!;
    public DateTime GeneratedAt { get; set; }

    public static DialogFeedbackDto FromEntity(DialogFeedback feedback) => new()
    {
        Summary = feedback.Summary,
        Content = feedback.Content,
        GeneratedAt = feedback.GeneratedAt
    };
}
