namespace SalesTrainer.Api.Features.Dialog.Models;

public sealed class DialogSessionDto
{
    public string Id { get; set; } = null!;
    public Guid BundleId { get; set; }
    public Guid ModeId { get; set; }
    public string Status { get; set; } = null!;
    public List<DialogMessageDto> Messages { get; set; } = [];
    public DialogFeedbackDto? Feedback { get; set; }
    public int XpEarned { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    public static DialogSessionDto FromEntity(DialogSession session) => new()
    {
        Id = session.Id,
        BundleId = session.BundleId,
        ModeId = session.ModeId,
        Status = session.Status.ToString().ToLowerInvariant(),
        Messages = session.Messages.Select(DialogMessageDto.FromEntity).ToList(),
        Feedback = session.Feedback != null ? DialogFeedbackDto.FromEntity(session.Feedback) : null,
        XpEarned = session.XpEarned,
        CreatedAt = session.CreatedAt,
        CompletedAt = session.CompletedAt
    };
}
