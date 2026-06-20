namespace Sellevate.Learning.Infrastructure.Ai;

public sealed class ChatMessageResult
{
    public string Content { get; set; } = string.Empty;
    public bool IsStopSignal { get; set; }
}
