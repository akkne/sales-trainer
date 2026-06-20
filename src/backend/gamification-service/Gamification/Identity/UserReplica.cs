namespace Sellevate.Gamification.Identity;

public sealed class UserReplica
{
    public Guid UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? AvatarKey { get; set; }
    public DateTime UpdatedAt { get; set; }
}
