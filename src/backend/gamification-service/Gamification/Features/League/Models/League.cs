namespace Sellevate.Gamification.Features.League.Models;

public sealed class League
{
    public Guid Id { get; set; }
    public string Tier { get; set; } = "bronze";
    public DateOnly WeekStartDate { get; set; }
    public DateOnly WeekEndDate { get; set; }
}
