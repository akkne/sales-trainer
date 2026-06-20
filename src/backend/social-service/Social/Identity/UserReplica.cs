namespace Sellevate.Social.Identity;

public sealed class UserReplica
{
    public Guid UserId { get; set; }
    public string Email { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string? AvatarKey { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
