namespace Sellevate.Ai.Features.Dialog.Models;

public sealed class UpdateModeRequestDto
{
    public string? Key { get; set; }
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? ChatSystemPrompt { get; set; }
    public string? FeedbackSystemPrompt { get; set; }
    public int? SortOrder { get; set; }
    public bool? IsActive { get; set; }
    public bool? VoiceEnabled { get; set; }
    public string? VoiceId { get; set; }
}
