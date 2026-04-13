namespace SalesTrainer.Api.Features.Dialog.Models;

public sealed class CreateModeRequestDto
{
    public string Key { get; set; } = null!;
    public string Title { get; set; } = null!;
    public string Description { get; set; } = null!;
    public string ChatSystemPrompt { get; set; } = null!;
    public string FeedbackSystemPrompt { get; set; } = null!;
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public bool VoiceEnabled { get; set; }
    public string? VoiceId { get; set; }
}
