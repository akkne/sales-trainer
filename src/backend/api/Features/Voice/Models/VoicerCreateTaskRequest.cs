namespace SalesTrainer.Api.Features.Voice.Models;

public sealed class VoicerCreateTaskRequest
{
    public string Text { get; set; } = null!;
    public string? TemplateUuid { get; set; }
    public VoicerTemplate? Template { get; set; }
}
