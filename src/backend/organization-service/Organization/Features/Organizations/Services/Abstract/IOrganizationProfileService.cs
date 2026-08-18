using Sellevate.Organization.Features.Organizations.Models;

namespace Sellevate.Organization.Features.Organizations.Services.Abstract;

/// <summary>
/// Reads and writes the caller's own organization profile. Unlike <see cref="IOrganizationService"/>,
/// there is no organization-id parameter anywhere on this interface: the target organization
/// comes solely from the scoped <c>ITenantContext</c>, which the caller cannot override
/// (docs/TENANCY/TENANCY.md §1.3).
///
/// <para>
/// Phase 40.29 added the three members below the upsert. They are the interview
/// (docs/ORGANIZATION_SERVICE.md, «Профиль как интервью»): what is still missing, one answer at a
/// time, and the promotion of a structure extracted from the customer's own material. They live on
/// this interface rather than on one of their own because they are all the same aggregate and the
/// same row, and a second service writing this row is exactly what 40.27 spent a decision avoiding.
/// </para>
/// </summary>
public interface IOrganizationProfileService
{
    Task<OrganizationProfileDto?> GetProfileAsync(CancellationToken cancellationToken = default);

    Task<OrganizationProfileDto> UpsertProfileAsync(
        UpdateOrganizationProfileRequestDto request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Phase 40.29. What the interview asks next, capped at <paramref name="questionLimit"/>.
    /// </summary>
    Task<OrganizationProfileGapsDto> GetGapsAsync(
        int questionLimit, CancellationToken cancellationToken = default);

    /// <summary>
    /// Phase 40.29. One answer to one question. Fields left <see langword="null"/> are untouched, so
    /// two people answering two different questions at the same time do not overwrite each other.
    /// </summary>
    Task<OrganizationProfileDto> PatchProfileAsync(
        PatchOrganizationProfileRequestDto request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Phase 40.29. What promoting <paramref name="draft"/> would do, computed and not saved.
    /// </summary>
    Task<OrganizationProfileDraftPreviewDto> PreviewDraftAsync(
        ExtractedProfileDraftDto draft, CancellationToken cancellationToken = default);

    /// <summary>
    /// Phase 40.29. Promotes a reviewed draft into the profile under the merge policy of
    /// <c>OrganizationProfileDraftMerger</c>, and returns what the interview still has to ask.
    /// </summary>
    Task<OrganizationProfileDraftAppliedDto> ApplyDraftAsync(
        ApplyOrganizationProfileDraftRequestDto request, CancellationToken cancellationToken = default);
}
