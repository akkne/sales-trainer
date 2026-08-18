namespace Sellevate.Organization.Features.Organizations.Models;

/// <summary>
/// Phase 40.29. What the merge would do to one field — the answer to «а что будет с тем, что я уже
/// вписал», shown before anything happens rather than discovered afterwards.
/// </summary>
/// <param name="Field">One of <c>OrganizationProfileFields</c>.</param>
/// <param name="Decision">One of the four <c>Decision*</c> constants on this record.</param>
/// <param name="CurrentValue">
/// The profile's value today, for the four single-valued fields; <see langword="null"/> for the three
/// additive ones, where «текущее значение» is a list and belongs on the profile response rather than
/// squashed into a string here.
/// </param>
/// <param name="SuggestedValue">What the draft proposes, on the same four fields.</param>
/// <param name="AddedItemCount">
/// How many entries the merge would add to an additive field. Zero everywhere else.
/// </param>
public sealed record OrganizationProfileFieldProposalDto(
    string Field,
    string Decision,
    string? CurrentValue,
    string? SuggestedValue,
    int AddedItemCount)
{
    /// <summary>The draft has nothing for this field, or nothing the profile does not already say.</summary>
    public const string DecisionUnchanged = "unchanged";

    /// <summary>
    /// The field was empty and the draft has something. Applied without asking — filling a blank
    /// destroys nothing, and an interview that asked permission to fill blanks would be the form
    /// again.
    /// </summary>
    public const string DecisionFill = "fill";

    /// <summary>
    /// Both have a value and they differ. <b>Nothing happens unless the caller names the field in
    /// <c>acceptedFields</c>.</b> This is the case the whole block turns on: a compliance officer's
    /// wording, a positioning sentence somebody argued about for a week, and one pasted deck. The
    /// screen shows both and a person decides.
    /// </summary>
    public const string DecisionConflict = "conflict";

    /// <summary>
    /// An additive field gains entries and loses none. Applied without asking, for the same reason
    /// <see cref="DecisionFill"/> is: nothing the customer wrote is touched.
    /// </summary>
    public const string DecisionExtend = "extend";
}
