using Microsoft.Extensions.Options;
using Sellevate.BuildingBlocks.Eventing;
using Sellevate.BuildingBlocks.Idempotency;
using Sellevate.BuildingBlocks.Messaging;
using Sellevate.Learning.Features.Content.Models;
using Sellevate.Learning.Infrastructure.Data;

namespace Sellevate.Learning.Eventing;

/// <summary>
/// Phase 40.19. Keeps <see cref="OrganizationProfileReplica"/> in step with the profile owned by
/// organization-service, so placeholder substitution never has to leave this service
/// (docs/TENANCY/BACKGROUND_JOBS.md).
///
/// <para>
/// Unlike identity's <c>OrganizationReplicaConsumer</c> this one runs in <b>tenant mode</b>, not
/// system mode: the profile lives inside a tenant rather than describing the registry, so the
/// envelope carries the organization, <see cref="KafkaConsumerBackgroundService.RequiresOrganization"/>
/// stays at its default <c>true</c>, and the write lands under the ordinary RLS policy with no
/// widening anywhere. A message that somehow arrives without a tenant is dead-lettered rather than
/// guessed at.
/// </para>
/// </summary>
internal sealed class OrganizationProfileConsumer : KafkaConsumerBackgroundService
{
    public OrganizationProfileConsumer(
        IOptions<KafkaSettings> settings,
        IServiceScopeFactory scopeFactory,
        IIdempotencyStore idempotencyStore,
        ILogger<OrganizationProfileConsumer> logger)
        : base(settings, scopeFactory, idempotencyStore, logger)
    {
    }

    protected override IReadOnlyCollection<string> Topics =>
    [
        BuildingBlocks.Eventing.Topics.OrganizationProfileUpdated,
    ];

    protected override async Task HandleAsync(
        EventEnvelope envelope,
        IServiceProvider scopedServices,
        CancellationToken cancellationToken)
    {
        var payload = envelope.DataAs<OrganizationProfileUpdatedEvent>();
        if (payload is null)
        {
            return;
        }

        var databaseContext = scopedServices.GetRequiredService<LearningDbContext>();

        var existing = await databaseContext.OrganizationProfileReplicas
            .FindAsync([payload.OrganizationId], cancellationToken);

        if (existing is null)
        {
            existing = new OrganizationProfileReplica { OrganizationId = payload.OrganizationId };
            databaseContext.OrganizationProfileReplicas.Add(existing);
        }

        existing.Product = payload.Product;
        existing.Icp = payload.Icp;
        existing.Tone = payload.Tone;
        // The jsonb columns are NOT NULL here as they are at the source, and System.Text.Json will
        // happily leave a non-nullable record property null when a field is absent from the message.
        // Coalescing keeps a truncated or older-shaped event from failing the whole projection.
        existing.ObjectionsJson = Coalesce(payload.ObjectionsJson, "[]");
        existing.ScriptJson = Coalesce(payload.ScriptJson, "[]");
        existing.GlossaryJson = Coalesce(payload.GlossaryJson, "{}");
        existing.BannedClaimsJson = Coalesce(payload.BannedClaimsJson, "[]");
        existing.UpdatedAt = payload.UpdatedAt;

        await databaseContext.SaveChangesAsync(cancellationToken);
    }

    private static string Coalesce(string? value, string fallback)
        => string.IsNullOrWhiteSpace(value) ? fallback : value;
}
