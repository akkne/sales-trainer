namespace Sellevate.Learning.Features.Techniques;

/// <summary>
/// The four technique mastery levels, the thresholds derived from them, and their display names.
///
/// <para>
/// The values are persisted in <c>UserTechniqueProgress.Level</c> and in <c>Technique.Difficulty</c>,
/// so they may be extended but never renumbered. <c>MasteredThresholdLevel</c> deliberately sits at
/// <see cref="Practitioner"/> rather than at the top: "mastered" counts a technique the learner can
/// use, while "master" counts one they have fully internalized.
/// </para>
/// </summary>
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
