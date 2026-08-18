using Sellevate.Organization.Common.Constants;
using Sellevate.Organization.Features.Organizations.Models;

namespace Sellevate.Organization.Features.Organizations.Services.Implementation;

/// <summary>
/// Phase 40.29. The merge policy — the whole reason 40.27 refused to let the pipeline write the
/// profile directly, now written down as one function.
///
/// <para>
/// <b>The rule, in one sentence: fill blanks, grow lists, never silently replace a human's words.</b>
/// The scenario the rule is built for is the one 40.27 named: a compliance officer types
/// <c>banned_claims</c> in March, a РОП pastes a new product deck in June, and the model reads the
/// deck's marketing copy as the company's position. An overwrite there is not a lost edit — it is a
/// persona that starts voicing a promise a lawyer forbade, discovered by the customer.
/// </para>
///
/// <para>
/// <b>Why the fields split into two groups and not three.</b> Four of them hold one value that a
/// suggestion would have to displace (<c>product</c>, <c>icp</c>, <c>tone</c>, <c>script_stages</c>),
/// so for those the merge either fills a blank or reports a conflict and waits for a person. Three of
/// them are collections that legitimately accumulate (<c>objections</c>, <c>glossary</c>,
/// <c>banned_claims</c>), so for those the merge is a union and there is nothing to consent to,
/// because nothing is lost.
/// </para>
///
/// <para>
/// <b><c>script_stages</c> is in the first group although it is a list.</b> It is an ordered sequence
/// describing one conversation, not a set: unioning a five-stage script with a seven-stage one
/// produces twelve stages in an order that describes no call anybody makes. So it is replaced whole
/// or not at all — which is also how the reviewer at the 40.27 checkpoint was looking at it.
/// </para>
///
/// <para>
/// <b><c>banned_claims</c> can be added to and never removed through this path, not even by consent.</b>
/// The union direction is the safe one: a compliance list that grows forbids more, and the failure
/// mode of a too-strict list is a persona that declines to say something it could have. Removing an
/// entry is a decision with a person's name on it, and it stays where it has always been — the
/// whole-profile form, filled in by somebody looking at the list. That is why the field is absent
/// from <c>OrganizationProfileFields.Overwritable</c>: there is no <c>acceptedFields</c> value that
/// deletes a banned claim, so no client bug and no stale second tab can produce one.
/// </para>
/// </summary>
internal static class OrganizationProfileDraftMerger
{
    /// <summary>
    /// Plans the merge without performing it. The preview route returns the plan; the apply route
    /// takes the same plan and saves <see cref="OrganizationProfileMergePlan.Merged"/>. One function
    /// for both, so the screen cannot describe a merge different from the one that runs.
    ///
    /// <para>
    /// The returned proposals are ordered by the interview's ordering
    /// (<c>OrganizationProfileGapCodes.All</c>) rather than by the order the per-field merges below
    /// happen to run in, for the same reason the gap list is: a screen that renders these top to bottom
    /// should read product-first however this method is later refactored.
    /// </para>
    /// </summary>
    /// <param name="profile">The profile as stored, or <see langword="null"/> if there is none yet.</param>
    /// <param name="draft">What was extracted from the material.</param>
    /// <param name="acceptedFields">
    /// Field names the caller has agreed may be overwritten. Names outside
    /// <c>OrganizationProfileFields.Overwritable</c> are ignored.
    /// </param>
    public static OrganizationProfileMergePlan Plan(
        OrganizationProfileDto? profile,
        ExtractedProfileDraftDto draft,
        IReadOnlyList<string>? acceptedFields)
    {
        ArgumentNullException.ThrowIfNull(draft);

        var accepted = new HashSet<string>(
            (acceptedFields ?? []).Where(OrganizationProfileFields.IsOverwritable),
            StringComparer.Ordinal);

        var proposals = new List<OrganizationProfileFieldProposalDto>();

        var mergedProduct = MergeSingleValue(
            OrganizationProfileFields.Product, profile?.Product, draft.Product, accepted, proposals);
        var mergedIcp = MergeSingleValue(
            OrganizationProfileFields.Icp, profile?.Icp, draft.Icp, accepted, proposals);
        var mergedTone = MergeSingleValue(
            OrganizationProfileFields.Tone, profile?.Tone, draft.Tone, accepted, proposals);

        var mergedScriptStages = MergeScriptStages(profile, draft, accepted, proposals);
        var mergedObjections = MergeObjections(profile, draft, proposals);
        var mergedGlossary = MergeGlossary(profile, draft, proposals);
        var mergedBannedClaims = MergeBannedClaims(profile, draft, proposals);

        var merged = new UpdateOrganizationProfileRequestDto(
            mergedProduct,
            mergedIcp,
            mergedObjections,
            mergedScriptStages,
            mergedTone,
            mergedGlossary,
            mergedBannedClaims);

        var orderedProposals = OrganizationProfileGapCodes.All
            .Select(code => proposals.First(proposal => proposal.Field == code))
            .ToList();

        return new OrganizationProfileMergePlan(merged, orderedProposals);
    }

    /// <summary>
    /// One value in, one value out. Blank profile plus a suggestion is a fill; two different values
    /// is a conflict that only <paramref name="acceptedFields"/> resolves; everything else changes
    /// nothing.
    ///
    /// <para>
    /// A conflict is <b>reported as a conflict whether or not it was accepted</b>. The screen has to be
    /// able to say «мы заменили ваше значение» afterwards, and a proposal that reported itself as
    /// unchanged once it had been applied would be the one place the audit of this merge could lie.
    /// </para>
    /// </summary>
    private static string? MergeSingleValue(
        string field,
        string? currentValue,
        string? suggestedValue,
        HashSet<string> acceptedFields,
        List<OrganizationProfileFieldProposalDto> proposals)
    {
        var current = Normalize(currentValue);
        var suggested = Normalize(suggestedValue);

        if (suggested is null || string.Equals(current, suggested, StringComparison.Ordinal))
        {
            proposals.Add(new OrganizationProfileFieldProposalDto(
                field,
                OrganizationProfileFieldProposalDto.DecisionUnchanged,
                current,
                suggested,
                AddedItemCount: 0));

            return current;
        }

        if (current is null)
        {
            proposals.Add(new OrganizationProfileFieldProposalDto(
                field,
                OrganizationProfileFieldProposalDto.DecisionFill,
                CurrentValue: null,
                suggested,
                AddedItemCount: 0));

            return suggested;
        }

        proposals.Add(new OrganizationProfileFieldProposalDto(
            field,
            OrganizationProfileFieldProposalDto.DecisionConflict,
            current,
            suggested,
            AddedItemCount: 0));

        return acceptedFields.Contains(field) ? suggested : current;
    }

    /// <summary>
    /// The script is one ordered sequence, so it is replaced whole or kept whole — see the class
    /// remarks for why a union would produce a call nobody makes.
    /// </summary>
    private static IReadOnlyList<string> MergeScriptStages(
        OrganizationProfileDto? profile,
        ExtractedProfileDraftDto draft,
        HashSet<string> acceptedFields,
        List<OrganizationProfileFieldProposalDto> proposals)
    {
        var currentStages = CleanList(profile?.ScriptStages);
        var suggestedStages = CleanList(draft.ScriptStages);

        if (suggestedStages.Count == 0 || currentStages.SequenceEqual(suggestedStages, StringComparer.Ordinal))
        {
            proposals.Add(new OrganizationProfileFieldProposalDto(
                OrganizationProfileFields.ScriptStages,
                OrganizationProfileFieldProposalDto.DecisionUnchanged,
                Describe(currentStages),
                Describe(suggestedStages),
                AddedItemCount: 0));

            return currentStages;
        }

        if (currentStages.Count == 0)
        {
            proposals.Add(new OrganizationProfileFieldProposalDto(
                OrganizationProfileFields.ScriptStages,
                OrganizationProfileFieldProposalDto.DecisionFill,
                CurrentValue: null,
                Describe(suggestedStages),
                suggestedStages.Count));

            return suggestedStages;
        }

        proposals.Add(new OrganizationProfileFieldProposalDto(
            OrganizationProfileFields.ScriptStages,
            OrganizationProfileFieldProposalDto.DecisionConflict,
            Describe(currentStages),
            Describe(suggestedStages),
            AddedItemCount: 0));

        return acceptedFields.Contains(OrganizationProfileFields.ScriptStages)
            ? suggestedStages
            : currentStages;
    }

    /// <summary>
    /// Union by objection text, case-insensitively. <b>An entry that is already there wins</b>, which
    /// is what preserves the <c>Frequency</c> the extraction cannot know and the answer a manager
    /// wrote from experience rather than from a deck.
    /// </summary>
    private static IReadOnlyList<OrganizationObjectionDto> MergeObjections(
        OrganizationProfileDto? profile,
        ExtractedProfileDraftDto draft,
        List<OrganizationProfileFieldProposalDto> proposals)
    {
        var mergedObjections = (profile?.Objections ?? [])
            .Where(objection => !string.IsNullOrWhiteSpace(objection.Text))
            .Select(objection => objection with { Text = objection.Text.Trim() })
            .ToList();

        var knownTexts = new HashSet<string>(
            mergedObjections.Select(objection => objection.Text),
            StringComparer.OrdinalIgnoreCase);

        var addedCount = 0;
        foreach (var suggested in draft.Objections ?? [])
        {
            var text = Normalize(suggested.Text);
            if (text is null || !knownTexts.Add(text))
            {
                continue;
            }

            mergedObjections.Add(new OrganizationObjectionDto(text, Frequency: null, Normalize(suggested.BestResponse)));
            addedCount++;
        }

        proposals.Add(new OrganizationProfileFieldProposalDto(
            OrganizationProfileFields.Objections,
            addedCount == 0
                ? OrganizationProfileFieldProposalDto.DecisionUnchanged
                : OrganizationProfileFieldProposalDto.DecisionExtend,
            CurrentValue: null,
            SuggestedValue: null,
            addedCount));

        return mergedObjections;
    }

    /// <summary>
    /// New terms are added; a term the customer has already defined keeps their definition. The
    /// glossary is precisely the field where the customer's word beats the model's — that is what it
    /// is for.
    /// </summary>
    private static IReadOnlyDictionary<string, string> MergeGlossary(
        OrganizationProfileDto? profile,
        ExtractedProfileDraftDto draft,
        List<OrganizationProfileFieldProposalDto> proposals)
    {
        var mergedGlossary = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in profile?.Glossary ?? new Dictionary<string, string>())
        {
            if (!string.IsNullOrWhiteSpace(entry.Key) && !string.IsNullOrWhiteSpace(entry.Value))
            {
                mergedGlossary[entry.Key.Trim()] = entry.Value.Trim();
            }
        }

        var addedCount = 0;
        foreach (var entry in draft.Glossary ?? new Dictionary<string, string>())
        {
            var term = Normalize(entry.Key);
            var definition = Normalize(entry.Value);
            if (term is null || definition is null || mergedGlossary.ContainsKey(term))
            {
                continue;
            }

            mergedGlossary[term] = definition;
            addedCount++;
        }

        proposals.Add(new OrganizationProfileFieldProposalDto(
            OrganizationProfileFields.Glossary,
            addedCount == 0
                ? OrganizationProfileFieldProposalDto.DecisionUnchanged
                : OrganizationProfileFieldProposalDto.DecisionExtend,
            CurrentValue: null,
            SuggestedValue: null,
            addedCount));

        return mergedGlossary;
    }

    /// <summary>
    /// Union, always, in one direction only. See the class remarks — this is the field the whole
    /// merge policy was designed around.
    /// </summary>
    private static IReadOnlyList<string> MergeBannedClaims(
        OrganizationProfileDto? profile,
        ExtractedProfileDraftDto draft,
        List<OrganizationProfileFieldProposalDto> proposals)
    {
        var mergedClaims = CleanList(profile?.BannedClaims);
        var knownClaims = new HashSet<string>(mergedClaims, StringComparer.OrdinalIgnoreCase);

        var addedCount = 0;
        foreach (var claim in CleanList(draft.BannedClaims))
        {
            if (!knownClaims.Add(claim))
            {
                continue;
            }

            mergedClaims.Add(claim);
            addedCount++;
        }

        proposals.Add(new OrganizationProfileFieldProposalDto(
            OrganizationProfileFields.BannedClaims,
            addedCount == 0
                ? OrganizationProfileFieldProposalDto.DecisionUnchanged
                : OrganizationProfileFieldProposalDto.DecisionExtend,
            CurrentValue: null,
            SuggestedValue: null,
            addedCount));

        return mergedClaims;
    }

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static List<string> CleanList(IEnumerable<string>? values)
        => (values ?? [])
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .ToList();

    /// <summary>
    /// A list rendered for the «есть ваше значение и предложение ИИ» comparison. Joined with the same
    /// separator <c>{{organization.script}}</c> renders with, so the reviewer compares the string the
    /// lessons will actually show.
    /// </summary>
    private static string? Describe(IReadOnlyList<string> stages)
        => stages.Count == 0 ? null : string.Join(" → ", stages);
}
