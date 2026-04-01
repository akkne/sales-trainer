namespace SalesTrainer.Api.Features.Auth;

public enum UserRole
{
    User = 0,
    Admin = 1,
    SuperAdmin = 2
}

public class User
{
    public Guid Id { get; set; }
    public string Email { get; set; } = "";
    public string? PasswordHash { get; set; }
    public string DisplayName { get; set; } = "";
    public string? GoogleId { get; set; }
    public DateTime CreatedAt { get; set; }
    public UserRole Role { get; set; } = UserRole.User;
}
