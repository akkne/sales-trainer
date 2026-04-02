namespace SalesTrainer.Api.Features.League;

public class LeagueMembership
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid LeagueId { get; set; }
    public int WeeklyXpAmount { get; set; }
    public int Rank { get; set; }
    public string? PromotionOutcome { get; set; }
}
