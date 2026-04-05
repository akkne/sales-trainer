namespace SalesTrainer.Api.Features.Dialog;

public class DialogModeDto
{
    public Guid Id { get; set; }
    public Guid BundleId { get; set; }
    public string Key { get; set; } = null!;
    public string Title { get; set; } = null!;
    public string Description { get; set; } = null!;
    public int SortOrder { get; set; }
    public bool IsActive { get; set; }

    public static DialogModeDto FromEntity(DialogMode mode) => new()
    {
        Id = mode.Id,
        BundleId = mode.BundleId,
        Key = mode.Key,
        Title = mode.Title,
        Description = mode.Description,
        SortOrder = mode.SortOrder,
        IsActive = mode.IsActive
    };
}

public class AdminDialogModeDto : DialogModeDto
{
    public string ChatSystemPrompt { get; set; } = null!;
    public string FeedbackSystemPrompt { get; set; } = null!;

    public static new AdminDialogModeDto FromEntity(DialogMode mode) => new()
    {
        Id = mode.Id,
        BundleId = mode.BundleId,
        Key = mode.Key,
        Title = mode.Title,
        Description = mode.Description,
        ChatSystemPrompt = mode.ChatSystemPrompt,
        FeedbackSystemPrompt = mode.FeedbackSystemPrompt,
        SortOrder = mode.SortOrder,
        IsActive = mode.IsActive
    };
}
