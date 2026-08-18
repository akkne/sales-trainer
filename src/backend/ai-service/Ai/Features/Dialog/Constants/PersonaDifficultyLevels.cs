namespace Sellevate.Ai.Features.Dialog.Constants;

/// <summary>
/// The persona difficulty vocabulary as it arrives on the wire from company-service and from the
/// assignment brief. Enum-like string set: anything not listed here is read as the middle level, so
/// a typo degrades quietly instead of failing, which is why the accepted spellings belong in one
/// place.
///
/// <para>
/// These are the keys only. <c>CompanyContextPromptBuilder</c> and
/// <c>AssignmentPracticePromptBuilder</c> each keep their own wording for what a level means to the
/// model, deliberately unshared — see the note on
/// <c>AssignmentPracticePromptBuilder.DescribeDifficultyToughness</c>.
/// </para>
/// </summary>
public static class PersonaDifficultyLevels
{
    /// <summary>Friendly, easily engaged.</summary>
    public const string Easy = "Easy";

    /// <summary>Sceptical, demanding, objects actively.</summary>
    public const string Hard = "Hard";
}
