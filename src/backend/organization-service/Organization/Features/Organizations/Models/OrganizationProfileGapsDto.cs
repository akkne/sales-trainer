namespace Sellevate.Organization.Features.Organizations.Models;

/// <summary>
/// Phase 40.29. The interview, one round of it.
///
/// <para>
/// <b><see cref="Questions"/> is capped and <see cref="TotalGapCount"/> is not.</b> The screen needs
/// both: three questions is what makes the thing answerable in one sitting, and «осталось ещё 4» is
/// what stops the third answer from feeling like the end when it is not. A capped list with no total
/// is a progress bar that lies.
/// </para>
/// </summary>
/// <param name="Questions">
/// The next few gaps, highest priority first, capped by the caller's limit. Empty means the profile
/// is as complete as this vocabulary can tell.
/// </param>
/// <param name="TotalGapCount">Every gap, including the ones not shown this round.</param>
/// <param name="BlockingGapCount">
/// How many of them are of the tier that stops content parameterization from working.
/// </param>
/// <param name="IsReadyForParameterization">
/// <see langword="true"/> when <see cref="BlockingGapCount"/> is zero — the profile now says enough
/// that a base lesson reads as the customer's own rather than as the neutral fallback
/// (docs/CONTENT_PARAMETERIZATION.md §2.1). Deliberately a narrower claim than «профиль заполнен»:
/// tone, banned claims and the glossary can all still be empty, and the two of them whose honest
/// answer may be «таких нет» would otherwise make the flag unreachable forever.
/// </param>
public sealed record OrganizationProfileGapsDto(
    IReadOnlyList<OrganizationProfileGapDto> Questions,
    int TotalGapCount,
    int BlockingGapCount,
    bool IsReadyForParameterization);
