namespace SalesTrainer.Api.Features.Voice;

public sealed class VoiceResponseDto
{
    public string Content { get; set; } = null!;
    public bool IsStopSignal { get; set; }
    public DateTime Timestamp { get; set; }
}
