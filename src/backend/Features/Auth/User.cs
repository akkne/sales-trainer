namespace SalesTrainer.Api.Features.Auth;

public class User
{
    public Guid Id { get; set; }
    public string Email { get; set; } = "";
    public string? PasswordHash { get; set; }
    public string DisplayName { get; set; } = "";
    public string? GoogleId { get; set; }
    public DateTime CreatedAt { get; set; }
}
