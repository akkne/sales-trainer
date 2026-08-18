using Sellevate.Organization.Common.Constants;
using Sellevate.Organization.Features.Organizations.Models;

namespace Sellevate.Organization.Features.Organizations.Services.Implementation;

/// <summary>
/// Phase 40.29. «Спрашивает только про пробелы» — the part that decides which gaps, and how many of
/// them at a time.
///
/// <para>
/// <b>Why the count is capped and the cap is small.</b> The roadmap's measure of success for this
/// block is a number: «5 минут вместо часа». The failure mode it names is also concrete — «30 пустых
/// полей никто не заполнит». Both are about the size of what a person is shown, not about how the
/// data is stored, so the cap is the feature and the ordering is the rest of it. Three questions is
/// what a РОП answers in the tab they are already in; seven is a form, and a form is what this block
/// exists to replace.
/// </para>
///
/// <para>
/// <b>Why the answer is deterministic and free.</b> Nothing here calls a model. Which fields are
/// empty is arithmetic, and the questions are fixed sentences (<see cref="OrganizationProfileGapCodes"/>).
/// The model's contribution to this block already happened, upstream, when it read the material —
/// paying it again to observe that a column is blank would be an expensive way to run
/// <c>string.IsNullOrWhiteSpace</c>.
/// </para>
/// </summary>
internal static class OrganizationProfileGapInspector
{
    /// <summary>
    /// Questions per round. Three, and the reasoning is in the class remarks.
    /// </summary>
    public const int DefaultQuestionLimit = 3;

    /// <summary>
    /// The most a caller may ask for. Seven is every question there is, so this is not a throttle —
    /// it is the statement that a client cannot ask for more questions than exist and then render an
    /// empty scroll.
    /// </summary>
    public const int MaximumQuestionLimit = 7;

    /// <summary>
    /// The fewest. A caller asking for zero or a negative number gets one question rather than an empty
    /// list, because an empty list is indistinguishable from «профиль заполнен» on the screen.
    /// </summary>
    public const int MinimumQuestionLimit = 1;

    /// <summary>
    /// Everything still missing from <paramref name="profile"/>, ordered by
    /// <c>OrganizationProfileGapCodes.All</c> and cut to <paramref name="questionLimit"/>.
    /// </summary>
    /// <param name="profile">
    /// The profile as stored, or <see langword="null"/> when the organization has never saved one —
    /// which is the state the roadmap's second bullet describes and therefore the one case that must
    /// not throw. A missing row and a row of empty strings ask exactly the same seven questions.
    /// </param>
    public static OrganizationProfileGapsDto Inspect(
        OrganizationProfileDto? profile,
        int questionLimit = DefaultQuestionLimit)
    {
        var missingCodes = FindMissingCodes(profile);

        var blockingGapCount = missingCodes.Count(OrganizationProfileGapCodes.IsBlocking);

        var boundedLimit = Math.Clamp(questionLimit, MinimumQuestionLimit, MaximumQuestionLimit);

        var questions = missingCodes
            .Take(boundedLimit)
            .Select(code => new OrganizationProfileGapDto(
                code,
                OrganizationProfileGapCodes.QuestionFor(code)!,
                OrganizationProfileGapCodes.PriorityFor(code)!))
            .ToList();

        return new OrganizationProfileGapsDto(
            questions,
            missingCodes.Count,
            blockingGapCount,
            IsReadyForParameterization: blockingGapCount == 0);
    }

    /// <summary>
    /// The codes, in asking order. Kept separate from the DTO assembly above so that the ordering
    /// lives in exactly one place — <c>OrganizationProfileGapCodes.All</c> — rather than being
    /// re-established by whoever adds the eighth field. The result is reordered by that list rather
    /// than returned in the order the checks below happen to run in, so reordering a check can never
    /// quietly reorder the interview.
    ///
    /// <para>
    /// Objections and script stages are counted against a threshold rather than tested for «any»,
    /// because one objection in the profile is what a persona then raises every session, and a persona
    /// with one objection is recognisable as a script — see
    /// <c>OrganizationProfileGapCodes.MinimumObjectionCount</c>.
    /// </para>
    /// </summary>
    private static List<string> FindMissingCodes(OrganizationProfileDto? profile)
    {
        var missing = new List<string>();

        if (string.IsNullOrWhiteSpace(profile?.Product))
        {
            missing.Add(OrganizationProfileGapCodes.Product);
        }

        if (string.IsNullOrWhiteSpace(profile?.Icp))
        {
            missing.Add(OrganizationProfileGapCodes.Icp);
        }

        if (CountNonEmpty(profile?.Objections?.Select(objection => objection.Text))
            < OrganizationProfileGapCodes.MinimumObjectionCount)
        {
            missing.Add(OrganizationProfileGapCodes.Objections);
        }

        if (CountNonEmpty(profile?.ScriptStages) < OrganizationProfileGapCodes.MinimumScriptStageCount)
        {
            missing.Add(OrganizationProfileGapCodes.ScriptStages);
        }

        if (string.IsNullOrWhiteSpace(profile?.Tone))
        {
            missing.Add(OrganizationProfileGapCodes.Tone);
        }

        if (CountNonEmpty(profile?.BannedClaims) == 0)
        {
            missing.Add(OrganizationProfileGapCodes.BannedClaims);
        }

        if (profile?.Glossary is null || profile.Glossary.Count == 0)
        {
            missing.Add(OrganizationProfileGapCodes.Glossary);
        }

        return OrganizationProfileGapCodes.All.Where(missing.Contains).ToList();
    }

    /// <summary>
    /// Entries that are actually text. A list holding three empty strings satisfies a length check
    /// and answers nothing, and the profile's jsonb columns accept exactly that.
    /// </summary>
    private static int CountNonEmpty(IEnumerable<string>? values)
        => values?.Count(value => !string.IsNullOrWhiteSpace(value)) ?? 0;
}
