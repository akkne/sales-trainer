using Sellevate.BuildingBlocks.ContentTemplating;

namespace Sellevate.Ai.Features.Organizations;

/// <summary>
/// Phase 40.19. The caller's own organization profile, for parameterizing persona prompts and for
/// the banned-claims rule appended to both the persona and the feedback prompt
/// (docs/TENANCY/CONTENT_MODEL.md §3).
/// </summary>
public interface IOrganizationProfileProvider
{
    /// <summary>
    /// Never returns <see langword="null"/>: no tenant, no profile row and an unfilled form all
    /// yield <see cref="OrganizationProfileSnapshot.Empty"/>, which appends nothing to a prompt and
    /// renders placeholders as the neutral base wording. A missing profile must not be able to fail
    /// a call that is already in progress.
    /// </summary>
    Task<OrganizationProfileSnapshot> GetCurrentAsync(CancellationToken cancellationToken = default);
}
