namespace Sellevate.Learning.Features.Techniques;

public static class TechniqueLevels
{
    public const int Novice = 1;
    public const int Practitioner = 2;
    public const int Expert = 3;
    public const int Master = 4;

    public const int MasteredThresholdLevel = Practitioner;
    public const int MasterThresholdLevel = Master;

    public static string ResolveDifficultyName(int difficulty)
    {
        return difficulty switch
        {
            Master => "Master",
            Expert => "Expert",
            Practitioner => "Practitioner",
            _ => "Novice",
        };
    }
}
