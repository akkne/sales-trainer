namespace SalesTrainer.Api.Features.Dialog;

public sealed class ChatMessageResult
{
    public string Content { get; set; } = null!;
    public bool IsStopSignal { get; set; }
}
