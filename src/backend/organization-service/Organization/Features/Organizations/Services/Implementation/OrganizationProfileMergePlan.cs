using Sellevate.Organization.Features.Organizations.Models;

namespace Sellevate.Organization.Features.Organizations.Services.Implementation;

/// <summary>
/// Phase 40.29. The planned merge: the profile as it would be, and the per-field account of how it
/// got that way.
///
/// <para>
/// Internal rather than a DTO because it is not a wire shape — <see cref="Merged"/> is an upsert
/// request the service feeds back into its own write path, which is what keeps the promotion from
/// becoming a second way of writing the profile row.
/// </para>
/// </summary>
internal sealed record OrganizationProfileMergePlan(
    UpdateOrganizationProfileRequestDto Merged,
    IReadOnlyList<OrganizationProfileFieldProposalDto> Proposals);
