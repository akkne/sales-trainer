namespace SalesTrainer.Api.Features.Dialog.Models;

public sealed class DialogSessionSummaryDto
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
