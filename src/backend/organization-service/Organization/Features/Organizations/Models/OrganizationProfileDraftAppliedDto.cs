namespace Sellevate.Organization.Features.Organizations.Models;

/// <summary>
/// Phase 40.29. What the promotion actually did, and what the interview asks next.
///
/// <para>
/// The gap list travels back with the write rather than being fetched afterwards, because the two
/// belong to the same moment: the screen that applied a draft has to turn immediately into the screen
/// that asks the remaining two questions, and a second round trip there is a spinner between «ИИ
/// заполнил профиль» and «остался один вопрос» — the exact seam where a five-minute flow becomes a
/// task for later.
/// </para>
/// </summary>
public sealed record OrganizationProfileDraftAppliedDto(
    OrganizationProfileDto Profile,
    IReadOnlyList<OrganizationProfileFieldProposalDto> AppliedFields,
    OrganizationProfileGapsDto Gaps);
