namespace SalesTrainer.Api.Features.Auth.Models;

public sealed class User
{
    public Guid Id { get; set; }
    public string Email { get; set; } = "";
    public string? PasswordHash { get; set; }
    public string DisplayName { get; set; } = "";
    public string? GoogleId { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsEmailVerified { get; set; }
    public UserRole Role { get; set; } = UserRole.User;
    public AvatarKind AvatarType { get; set; } = AvatarKind.Default;
    public string? AvatarKey { get; set; }
    public int DefaultAvatarIndex { get; set; }
}
