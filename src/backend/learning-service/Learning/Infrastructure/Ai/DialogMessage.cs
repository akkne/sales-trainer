namespace Sellevate.Learning.Infrastructure.Ai;

public sealed class DialogMessage
{
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
