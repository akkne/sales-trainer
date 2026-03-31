namespace SalesTrainer.Api.Features.League;

public class League
{
    public Guid Id { get; set; }
    public string Tier { get; set; } = "bronze";
    public DateOnly WeekStartDate { get; set; }
    public DateOnly WeekEndDate { get; set; }
}
