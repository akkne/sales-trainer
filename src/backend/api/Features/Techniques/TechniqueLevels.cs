namespace SalesTrainer.Api.Features.Techniques;

public static class TechniqueLevels
{
    public const int Novice = 1;
    public const int Practitioner = 2;
    public const int Expert = 3;
    public const int Master = 4;

    public const int MasteredThresholdLevel = Practitioner;
    public const int MasterThresholdLevel = Master;

    public static string ResolveLevelName(int level, int masteryPercent)
    {
        return level switch
        {
            Master => "Master",
            Expert => masteryPercent >= 95 ? "Expert+" : "Expert",
            Practitioner => masteryPercent >= 85 ? "Practitioner+" : "Practitioner",
            _ => masteryPercent >= 50 ? "Novice+" : "Novice",
        };
    }
}
