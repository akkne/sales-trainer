namespace SalesTrainer.Api.Features.Dialog;

public class DialogSessionDto
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

public class DialogSessionSummaryDto
{
    public string Id { get; set; } = null!;
    public Guid BundleId { get; set; }
    public Guid ModeId { get; set; }
    public string ModeTitle { get; set; } = null!;
    public string BundleTitle { get; set; } = null!;
    public string Status { get; set; } = null!;
    public int MessageCount { get; set; }
    public int XpEarned { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    public static DialogSessionSummaryDto FromEntity(DialogSession session, string bundleTitle, string modeTitle) => new()
    {
        Id = session.Id,
        BundleId = session.BundleId,
        ModeId = session.ModeId,
        ModeTitle = modeTitle,
        BundleTitle = bundleTitle,
        Status = session.Status.ToString().ToLowerInvariant(),
        MessageCount = session.Messages.Count,
        XpEarned = session.XpEarned,
        CreatedAt = session.CreatedAt,
        CompletedAt = session.CompletedAt
    };
}

public class DialogMessageDto
{
    public string Role { get; set; } = null!;
    public string Content { get; set; } = null!;
    public DateTime Timestamp { get; set; }
    public bool IsStopSignal { get; set; }

    public static DialogMessageDto FromEntity(DialogMessage message) => new()
    {
        Role = message.Role,
        Content = message.Content,
        Timestamp = message.Timestamp,
        IsStopSignal = message.IsStopSignal
    };
}

public class DialogFeedbackDto
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
