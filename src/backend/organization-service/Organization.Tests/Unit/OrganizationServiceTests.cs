using FluentAssertions;
using NSubstitute;
using NUnit.Framework;
using Sellevate.BuildingBlocks.Eventing;
using Sellevate.BuildingBlocks.Messaging;
using Sellevate.Organization.Eventing;
using Sellevate.Organization.Features.Organizations.Exceptions;
using Sellevate.Organization.Features.Organizations.Models;
using Sellevate.Organization.Features.Organizations.Services.Implementation;
using Sellevate.Organization.Infrastructure.Data;
using Sellevate.Organization.Tests.Helpers;

namespace Sellevate.Organization.Tests.Unit;

[TestFixture]
public sealed class OrganizationServiceTests
{
    private OrganizationDbContext _databaseContext = null!;
    private IEventPublisher _eventPublisher = null!;
    private OrganizationService _organizationService = null!;

    [SetUp]
    public void SetUp()
    {
        _databaseContext = TestOrganizationDatabaseFactory.CreateInMemory();
        _eventPublisher = Substitute.For<IEventPublisher>();
        _organizationService = new OrganizationService(_databaseContext, _eventPublisher);
    }

    [TearDown]
    public void TearDown() => _databaseContext.Dispose();

    [Test]
    public async Task CreateOrganizationAsync_persists_the_organization_and_publishes_organization_created()
    {
        var result = await _organizationService.CreateOrganizationAsync(
            new CreateOrganizationRequestDto("Acme Sales", null));

        result.Name.Should().Be("Acme Sales");
        result.Slug.Should().Be("acme-sales");
        result.Status.Should().Be(nameof(OrganizationStatus.Active));

        (await _databaseContext.Organizations.FindAsync(result.Id)).Should().NotBeNull();

        await _eventPublisher.Received(1).PublishAsync(
            Topics.OrganizationCreated,
            result.Id.ToString(),
            Topics.OrganizationCreated,
            Arg.Is<OrganizationCreatedEvent>(created =>
                created.OrganizationId == result.Id && created.Name == "Acme Sales" && created.Slug == "acme-sales"),
            version: 1,
            cancellationToken: Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task CreateOrganizationAsync_normalizes_an_explicit_slug()
    {
        var result = await _organizationService.CreateOrganizationAsync(
            new CreateOrganizationRequestDto("Acme Sales", "  Acme  Sales!! "));

        result.Slug.Should().Be("acme-sales");
    }

    [Test]
    public async Task CreateOrganizationAsync_throws_OrganizationSlugConflictException_when_slug_already_taken()
    {
        await _organizationService.CreateOrganizationAsync(new CreateOrganizationRequestDto("Acme Sales", "acme"));
        _eventPublisher.ClearReceivedCalls();

        var act = async () => await _organizationService.CreateOrganizationAsync(
            new CreateOrganizationRequestDto("Acme Sales Two", "acme"));

        await act.Should().ThrowAsync<OrganizationSlugConflictException>();
        await _eventPublisher.DidNotReceiveWithAnyArgs().PublishAsync<OrganizationCreatedEvent>(
            default!, default!, default!, default!, cancellationToken: default);
    }

    [Test]
    public void CreateOrganizationAsync_throws_ArgumentException_when_name_is_blank()
    {
        var act = async () => await _organizationService.CreateOrganizationAsync(
            new CreateOrganizationRequestDto("   ", null));

        act.Should().ThrowAsync<ArgumentException>();
    }

    [Test]
    public async Task ListOrganizationsAsync_returns_organizations_newest_first()
    {
        var older = await _organizationService.CreateOrganizationAsync(new CreateOrganizationRequestDto("Older", null));
        var newer = await _organizationService.CreateOrganizationAsync(new CreateOrganizationRequestDto("Newer", null));

        var organizations = await _organizationService.ListOrganizationsAsync();

        organizations.Select(organization => organization.Id).Should().ContainInOrder(newer.Id, older.Id);
    }

    [Test]
    public async Task GetOrganizationAsync_returns_null_when_not_found()
    {
        var result = await _organizationService.GetOrganizationAsync(Guid.NewGuid());

        result.Should().BeNull();
    }

    [Test]
    public async Task UpdateOrganizationAsync_renames_and_publishes_organization_updated()
    {
        var created = await _organizationService.CreateOrganizationAsync(new CreateOrganizationRequestDto("Acme", null));

        var updated = await _organizationService.UpdateOrganizationAsync(
            created.Id, new UpdateOrganizationRequestDto("Acme Renamed", "acme-renamed"));

        updated.Should().NotBeNull();
        updated!.Name.Should().Be("Acme Renamed");
        updated.Slug.Should().Be("acme-renamed");

        await _eventPublisher.Received(1).PublishAsync(
            Topics.OrganizationUpdated,
            created.Id.ToString(),
            Topics.OrganizationUpdated,
            Arg.Is<OrganizationUpdatedEvent>(updatedEvent => updatedEvent.OrganizationId == created.Id && updatedEvent.Name == "Acme Renamed"),
            version: 1,
            cancellationToken: Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task UpdateOrganizationAsync_returns_null_when_the_organization_does_not_exist()
    {
        var result = await _organizationService.UpdateOrganizationAsync(
            Guid.NewGuid(), new UpdateOrganizationRequestDto("Whoever", null));

        result.Should().BeNull();
    }

    [Test]
    public async Task SuspendOrganizationAsync_sets_status_suspended_and_publishes_organization_suspended()
    {
        var created = await _organizationService.CreateOrganizationAsync(new CreateOrganizationRequestDto("Acme", null));

        var suspended = await _organizationService.SuspendOrganizationAsync(created.Id);

        suspended!.Status.Should().Be(nameof(OrganizationStatus.Suspended));

        await _eventPublisher.Received(1).PublishAsync(
            Topics.OrganizationSuspended,
            created.Id.ToString(),
            Topics.OrganizationSuspended,
            Arg.Is<OrganizationSuspendedEvent>(suspendedEvent => suspendedEvent.OrganizationId == created.Id),
            version: 1,
            cancellationToken: Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ReactivateOrganizationAsync_sets_status_active_and_publishes_organization_updated()
    {
        var created = await _organizationService.CreateOrganizationAsync(new CreateOrganizationRequestDto("Acme", null));
        await _organizationService.SuspendOrganizationAsync(created.Id);
        _eventPublisher.ClearReceivedCalls();

        var reactivated = await _organizationService.ReactivateOrganizationAsync(created.Id);

        reactivated!.Status.Should().Be(nameof(OrganizationStatus.Active));

        await _eventPublisher.Received(1).PublishAsync(
            Topics.OrganizationUpdated,
            created.Id.ToString(),
            Topics.OrganizationUpdated,
            Arg.Is<OrganizationUpdatedEvent>(updatedEvent => updatedEvent.Status == nameof(OrganizationStatus.Active)),
            version: 1,
            cancellationToken: Arg.Any<CancellationToken>());
    }
}
