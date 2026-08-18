namespace Sellevate.Organization.Features.Organizations.Models;

/// <summary>
/// Phase 40.29. «Перенести в профиль» — the draft, plus the short list of fields the person agreed to
/// let it overwrite.
/// </summary>
/// <param name="Draft">The extracted structure, as reviewed at the 40.27 checkpoint.</param>
/// <param name="AcceptedFields">
/// Names from <c>OrganizationProfileFields.Overwritable</c>. Omitted or empty is the safe default and
/// the expected case: blanks are filled, additive fields grow, and every field that already had a
/// value keeps it. Unknown names are dropped rather than rejected — 40.28's rule for unknown codes,
/// and here it also means a client that learns a new field name cannot make an older server overwrite
/// something by accident.
/// </param>
public sealed record ApplyOrganizationProfileDraftRequestDto(
    ExtractedProfileDraftDto? Draft,
    IReadOnlyList<string>? AcceptedFields);
