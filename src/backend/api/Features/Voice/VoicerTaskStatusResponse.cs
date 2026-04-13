namespace SalesTrainer.Api.Features.Voice;

public sealed class VoicerTaskStatusResponse
{
    public int? TaskId { get; set; }
    public string? Status { get; set; }
    public string? StatusLabel { get; set; }
    public string? CreatedAt { get; set; }
}
