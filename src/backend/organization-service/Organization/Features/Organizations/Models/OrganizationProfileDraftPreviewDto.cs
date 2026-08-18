namespace Sellevate.Organization.Features.Organizations.Models;

/// <summary>
/// Phase 40.29. The «вот что ИИ предлагает» screen, computed and thrown away — this response is the
/// product of a route that writes nothing.
///
/// <para>
/// A separate route rather than a <c>dryRun</c> flag on the apply route: a request that sometimes
/// writes and sometimes does not is one boolean away from writing when nobody meant it to, and the
/// thing on the other side of that mistake is the customer's compliance list. Both routes plan the
/// merge with the same function, so the preview cannot describe a merge different from the one that
/// then runs.
/// </para>
/// </summary>
/// <param name="Fields">One entry per profile field, in <c>OrganizationProfileGapCodes.All</c> order.</param>
/// <param name="ConflictCount">
/// How many fields would need an explicit decision. The screen's whole job when this is zero is a
/// single «применить» button.
/// </param>
/// <param name="GapsAfterApply">
/// What the interview would still have to ask about once everything on offer had been applied. This
/// is the roadmap's «спрашивает только про пробелы», computed before the customer has committed to
/// anything: it is what lets the screen say «ИИ заполнил пять полей, остались два вопроса».
/// </param>
public sealed record OrganizationProfileDraftPreviewDto(
    IReadOnlyList<OrganizationProfileFieldProposalDto> Fields,
    int ConflictCount,
    OrganizationProfileGapsDto GapsAfterApply);
