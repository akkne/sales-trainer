namespace SalesTrainer.Api.Features.League.Models;

public sealed class LeagueSettings
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int MaximumLeagueParticipantCount { get; set; } = 30;
    public int PromotionZoneSize { get; set; } = 10;
    public int DemotionZoneSize { get; set; } = 5;
}
