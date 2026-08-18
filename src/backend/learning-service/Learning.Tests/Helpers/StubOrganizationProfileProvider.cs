using Sellevate.BuildingBlocks.ContentTemplating;
using Sellevate.Learning.Features.Content.Services.Abstract;

namespace Sellevate.Learning.Tests.Helpers;

/// <summary>
/// Phase 40.19. Hands every caller the empty profile, which is what an organization that has not
/// filled the form in has. Used by the existing unit tests, none of which are about substitution:
/// an NSubstitute double would return a null <c>Task</c> and turn every one of them into a
/// NullReferenceException at the first await.
/// </summary>
internal sealed class StubOrganizationProfileProvider(OrganizationProfileSnapshot? profile = null)
    : IOrganizationProfileProvider
{
    private readonly OrganizationProfileSnapshot _profile = profile ?? OrganizationProfileSnapshot.Empty;

    public Task<OrganizationProfileSnapshot> GetCurrentAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(_profile);
}
