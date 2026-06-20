namespace Sellevate.Gamification.Features.Gamification.Models;

public sealed class GamificationSettings
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int DailyXpGoal { get; set; } = 100;
    public int WeeklyXpGoal { get; set; } = 500;
    public double DialogXpMultiplier { get; set; } = 1.0;
    public int DialogWeightConfidence { get; set; } = 25;
    public int DialogWeightStructure { get; set; } = 25;
    public int DialogWeightObjection { get; set; } = 25;
    public int DialogWeightGoal { get; set; } = 25;
}
