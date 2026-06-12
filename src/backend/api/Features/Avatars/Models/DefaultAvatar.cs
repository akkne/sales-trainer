namespace SalesTrainer.Api.Features.Avatars.Models;

public sealed class DefaultAvatar
{
    public Guid Id { get; set; }
    public int Index { get; set; }
    public string ObjectKey { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}
