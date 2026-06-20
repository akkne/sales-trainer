namespace Sellevate.Identity.Features.Auth.Models;

public sealed class EmailVerificationCode
{
    public Guid Id { get; set; }
    public string Email { get; set; } = "";
    public string CodeHash { get; set; } = "";
    public DateTime ExpiresAt { get; set; }
    public int AttemptCount { get; set; }
    public DateTime CreatedAt { get; set; }
}
