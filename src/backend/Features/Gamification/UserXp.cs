namespace SalesTrainer.Api.Features.Gamification;

public class UserXp
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public int Amount { get; set; }
    public string Source { get; set; } = "";
    public DateTime EarnedAt { get; set; }
}
