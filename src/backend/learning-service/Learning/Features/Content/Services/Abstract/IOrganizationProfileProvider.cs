using Sellevate.BuildingBlocks.ContentTemplating;

namespace Sellevate.Learning.Features.Content.Services.Abstract;

/// <summary>
/// Phase 40.19. The caller's own organization profile, for resolving <c>{{organization.*}}</c>
/// placeholders on the read path (docs/TENANCY/CONTENT_MODEL.md §3).
/// </summary>
public interface IOrganizationProfileProvider
{
    /// <summary>
    /// Never returns <see langword="null"/>: a caller with no tenant, an organization that has not
    /// filled the form in, and an organization whose replica has not arrived yet all get
    /// <see cref="OrganizationProfileSnapshot.Empty"/>, which renders as the neutral base wording.
    /// A missing profile must not be able to fail a lesson.
    /// </summary>
    Task<OrganizationProfileSnapshot> GetCurrentAsync(CancellationToken cancellationToken = default);
}
