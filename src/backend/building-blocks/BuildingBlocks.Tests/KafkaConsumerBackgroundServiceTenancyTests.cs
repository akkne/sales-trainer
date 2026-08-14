using FluentAssertions;
using NUnit.Framework;
using Sellevate.BuildingBlocks.Eventing;
using Sellevate.BuildingBlocks.Messaging;
using Sellevate.BuildingBlocks.Tenancy;

namespace Sellevate.BuildingBlocks.Tests;

/// <summary>
/// Covers <see cref="KafkaConsumerBackgroundService.ApplyTenantContext"/> — the base consumer's
/// pre-handler step that sets the tenant context from the envelope and fails a message that
/// carries no organization, per docs/TENANCY/TENANCY.md §1.6/§1.7. Exercised directly against
/// the extracted, dependency-free method rather than through the full Kafka consume loop, the
/// same way <see cref="EventMessageProcessorTests"/> covers the surrounding retry/dead-letter
/// logic without a broker.
/// </summary>
[TestFixture]
public sealed class KafkaConsumerBackgroundServiceTenancyTests
{
    private sealed record SamplePayload(Guid UserId);

    [Test]
    public void ApplyTenantContext_SetsOrganization_WhenEnvelopeCarriesOne()
    {
        var organizationId = Guid.NewGuid();
        var envelope = EventEnvelope.Create(
            Topics.ExerciseCompleted, new SamplePayload(Guid.NewGuid()), organizationId: organizationId);
        var tenantContext = new TenantContext();

        KafkaConsumerBackgroundService.ApplyTenantContext(
            tenantContext, envelope, requiresOrganization: true, consumerName: "TestConsumer");

        tenantContext.OrganizationId.Should().Be(organizationId);
        tenantContext.IsSystem.Should().BeFalse();
    }

    [Test]
    public void ApplyTenantContext_Throws_WhenOrganizationIsMissingAndRequired()
    {
        var envelope = EventEnvelope.Create(Topics.ExerciseCompleted, new SamplePayload(Guid.NewGuid()));
        var tenantContext = new TenantContext();

        var act = () => KafkaConsumerBackgroundService.ApplyTenantContext(
            tenantContext, envelope, requiresOrganization: true, consumerName: "TestConsumer");

        act.Should().Throw<InvalidOperationException>().WithMessage("*TestConsumer*");
        tenantContext.OrganizationId.Should().BeNull();
        tenantContext.IsSystem.Should().BeFalse();
    }

    [Test]
    public void ApplyTenantContext_EntersSystemMode_WhenOrganizationIsMissingButNotRequired()
    {
        var envelope = EventEnvelope.Create(Topics.UserRegistered, new SamplePayload(Guid.NewGuid()));
        var tenantContext = new TenantContext();

        KafkaConsumerBackgroundService.ApplyTenantContext(
            tenantContext, envelope, requiresOrganization: false, consumerName: "UserReplicaConsumer");

        tenantContext.IsSystem.Should().BeTrue();
        tenantContext.OrganizationId.Should().BeNull();
    }

    [Test]
    public void ApplyTenantContext_IsIdempotent_AcrossRetriesWithTheSameOrganization()
    {
        var organizationId = Guid.NewGuid();
        var envelope = EventEnvelope.Create(
            Topics.ExerciseCompleted, new SamplePayload(Guid.NewGuid()), organizationId: organizationId);
        var tenantContext = new TenantContext();

        KafkaConsumerBackgroundService.ApplyTenantContext(
            tenantContext, envelope, requiresOrganization: true, consumerName: "TestConsumer");
        var act = () => KafkaConsumerBackgroundService.ApplyTenantContext(
            tenantContext, envelope, requiresOrganization: true, consumerName: "TestConsumer");

        act.Should().NotThrow();
        tenantContext.OrganizationId.Should().Be(organizationId);
    }

    [Test]
    public void ApplyTenantContext_IsIdempotent_AcrossRetriesInSystemMode()
    {
        var envelope = EventEnvelope.Create(Topics.UserRegistered, new SamplePayload(Guid.NewGuid()));
        var tenantContext = new TenantContext();

        KafkaConsumerBackgroundService.ApplyTenantContext(
            tenantContext, envelope, requiresOrganization: false, consumerName: "UserReplicaConsumer");
        var act = () => KafkaConsumerBackgroundService.ApplyTenantContext(
            tenantContext, envelope, requiresOrganization: false, consumerName: "UserReplicaConsumer");

        act.Should().NotThrow();
        tenantContext.IsSystem.Should().BeTrue();
    }
}
