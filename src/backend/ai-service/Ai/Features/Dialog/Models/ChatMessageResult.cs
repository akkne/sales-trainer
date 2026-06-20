namespace Sellevate.Ai.Features.Dialog.Models;

public sealed class ChatMessageResult
{
    public string Content { get; set; } = null!;
    public bool IsStopSignal { get; set; }
}
