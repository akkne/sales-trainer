using Microsoft.Extensions.Options;
using Sellevate.BuildingBlocks.Eventing;
using Sellevate.BuildingBlocks.Idempotency;
using Sellevate.BuildingBlocks.Messaging;
using Sellevate.Identity.Features.Organizations.Models;
using Sellevate.Identity.Infrastructure.Data;

namespace Sellevate.Identity.Eventing;

/// <summary>
/// Keeps <see cref="OrganizationReplica"/> in step with the tenant registry owned by
/// organization-service (Phase 40.9). Without it, suspending an organization would have no effect
/// on the one service that mints tokens. The projection itself lives in
/// <see cref="OrganizationReplicaProjector"/>; this class is only the transport.
/// </summary>
internal sealed class OrganizationReplicaConsumer : KafkaConsumerBackgroundService
{
    public OrganizationReplicaConsumer(
        IOptions<KafkaSettings> settings,
        IServiceScopeFactory scopeFactory,
        IIdempotencyStore idempotencyStore,
        ILogger<OrganizationReplicaConsumer> logger)
        : base(settings, scopeFactory, idempotencyStore, logger)
    {
    }

    protected override IReadOnlyCollection<string> Topics =>
    [
        BuildingBlocks.Eventing.Topics.OrganizationCreated,
        BuildingBlocks.Eventing.Topics.OrganizationUpdated,
        BuildingBlocks.Eventing.Topics.OrganizationSuspended,
    ];

    /// <summary>
    /// These events describe the tenant registry rather than living inside one tenant — the
    /// organization is the payload, not the context. Requiring an envelope
    /// <c>organizationId</c> here would reject every message organization-service publishes, so
    /// this is the explicit, auditable opt-out the base class asks for
    /// (docs/TENANCY/TENANCY.md §1.7).
    /// </summary>
    protected override bool RequiresOrganization => false;

    protected override Task HandleAsync(
        EventEnvelope envelope,
        IServiceProvider scopedServices,
        CancellationToken cancellationToken)
        => OrganizationReplicaProjector.ApplyAsync(
            envelope,
            scopedServices.GetRequiredService<IdentityDbContext>(),
            cancellationToken);
}
