namespace SalesTrainer.Api.Features.Dialog.Models;

public sealed class AdminDialogModeDto : DialogModeDto
{
    public string ChatSystemPrompt { get; set; } = null!;
    public string FeedbackSystemPrompt { get; set; } = null!;
    public string? VoiceId { get; set; }

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
        IsActive = mode.IsActive,
        VoiceEnabled = mode.VoiceEnabled,
        VoiceId = mode.VoiceId
    };
}
