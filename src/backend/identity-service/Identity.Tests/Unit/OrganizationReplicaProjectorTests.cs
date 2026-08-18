using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using Sellevate.BuildingBlocks.Eventing;
using Sellevate.Identity.Eventing;
using Sellevate.Identity.Features.Organizations.Models;
using Sellevate.Identity.Tests.Helpers;

namespace Sellevate.Identity.Tests.Unit;

/// <summary>
/// Phase 40.9 — the projection that carries organization-service's registry into identity-db.
/// It is what makes a suspension reach the service that mints tokens, so each of the three events
/// is checked rather than assumed.
/// </summary>
[TestFixture]
public class OrganizationReplicaProjectorTests
{
    [Test]
    public async Task OrganizationCreated_AddsAnActiveReplica()
    {
        await using var databaseContext = InMemoryDbContextFactory.Create();
        var organizationId = Guid.NewGuid();

        await OrganizationReplicaProjector.ApplyAsync(
            EventEnvelope.Create(
                Topics.OrganizationCreated,
                new OrganizationCreatedEvent(organizationId, "Acme Sales", "acme-sales")),
            databaseContext,
            CancellationToken.None);

        var replica = await databaseContext.OrganizationReplicas.SingleAsync();
        replica.OrganizationId.Should().Be(organizationId);
        replica.Name.Should().Be("Acme Sales");
        replica.Slug.Should().Be("acme-sales");
        replica.Status.Should().Be(OrganizationReplicaStatus.Active);
    }

    [Test]
    public async Task OrganizationSuspended_MarksAnExistingReplicaSuspended()
    {
        await using var databaseContext = InMemoryDbContextFactory.Create();
        var organizationId = Guid.NewGuid();

        await OrganizationReplicaProjector.ApplyAsync(
            EventEnvelope.Create(
                Topics.OrganizationCreated,
                new OrganizationCreatedEvent(organizationId, "Acme Sales", "acme-sales")),
            databaseContext,
            CancellationToken.None);

        await OrganizationReplicaProjector.ApplyAsync(
            EventEnvelope.Create(
                Topics.OrganizationSuspended,
                new OrganizationSuspendedEvent(organizationId, "Acme Sales")),
            databaseContext,
            CancellationToken.None);

        var replica = await databaseContext.OrganizationReplicas.SingleAsync();
        replica.Status.Should().Be(OrganizationReplicaStatus.Suspended);
        replica.Slug.Should().Be("acme-sales", "the suspension event carries no slug and must not erase it");
    }

    [Test]
    public async Task OrganizationSuspended_ForAnUnseenOrganization_StillRecordsTheSuspension()
    {
        await using var databaseContext = InMemoryDbContextFactory.Create();
        var organizationId = Guid.NewGuid();

        await OrganizationReplicaProjector.ApplyAsync(
            EventEnvelope.Create(
                Topics.OrganizationSuspended,
                new OrganizationSuspendedEvent(organizationId, "Never Seen")),
            databaseContext,
            CancellationToken.None);

        var replica = await databaseContext.OrganizationReplicas.SingleAsync();
        replica.Status.Should().Be(
            OrganizationReplicaStatus.Suspended,
            "a missing create event must never let a suspension read as 'active'");
    }

    [Test]
    public async Task OrganizationUpdated_CarriesTheStatusBackToActiveOnReactivation()
    {
        await using var databaseContext = InMemoryDbContextFactory.Create();
        var organizationId = Guid.NewGuid();

        await OrganizationReplicaProjector.ApplyAsync(
            EventEnvelope.Create(
                Topics.OrganizationSuspended,
                new OrganizationSuspendedEvent(organizationId, "Acme Sales")),
            databaseContext,
            CancellationToken.None);

        await OrganizationReplicaProjector.ApplyAsync(
            EventEnvelope.Create(
                Topics.OrganizationUpdated,
                new OrganizationUpdatedEvent(organizationId, "Acme Sales", "acme-sales", "Active")),
            databaseContext,
            CancellationToken.None);

        var replica = await databaseContext.OrganizationReplicas.SingleAsync();
        replica.Status.Should().Be(OrganizationReplicaStatus.Active);
    }

    [Test]
    public async Task UnrelatedEvent_IsIgnored()
    {
        await using var databaseContext = InMemoryDbContextFactory.Create();

        await OrganizationReplicaProjector.ApplyAsync(
            EventEnvelope.Create(Topics.UserRegistered, new { userId = Guid.NewGuid() }),
            databaseContext,
            CancellationToken.None);

        (await databaseContext.OrganizationReplicas.CountAsync()).Should().Be(0);
    }
}
