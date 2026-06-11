namespace SalesTrainer.Api.Infrastructure.Configuration;

public sealed class GamificationConfiguration
{
    public const string SectionName = "Gamification";

    public int DailyXpGoal { get; init; } = 100;
}
