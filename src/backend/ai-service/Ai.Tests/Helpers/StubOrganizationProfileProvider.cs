using Sellevate.Ai.Features.Organizations;
using Sellevate.BuildingBlocks.ContentTemplating;

namespace Sellevate.Ai.Tests.Helpers;

/// <summary>
/// Phase 40.19. Hands every caller the empty profile, which is what an organization that has not
/// filled the form in has — so the existing prompt-composition tests keep asserting exactly the
/// prompt they asserted before this phase. An NSubstitute double would return a null <c>Task</c>
/// and turn each of them into a NullReferenceException at the first await.
/// </summary>
internal sealed class StubOrganizationProfileProvider(OrganizationProfileSnapshot? profile = null)
    : IOrganizationProfileProvider
{
    private readonly OrganizationProfileSnapshot _profile = profile ?? OrganizationProfileSnapshot.Empty;

    public Task<OrganizationProfileSnapshot> GetCurrentAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(_profile);
}
