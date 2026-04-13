namespace SalesTrainer.Api.Features.Dialog.Models;

public sealed class DialogMessageDto
{
    public string Role { get; set; } = null!;
    public string Content { get; set; } = null!;
    public DateTime Timestamp { get; set; }
    public bool IsStopSignal { get; set; }

    public static DialogMessageDto FromEntity(DialogMessage message) => new()
    {
        Role = message.Role,
        Content = message.Content,
        Timestamp = message.Timestamp,
        IsStopSignal = message.IsStopSignal
    };
}
