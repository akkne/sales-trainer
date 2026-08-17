using Microsoft.Extensions.Options;
using Sellevate.Ai.Features.Organizations;
using Sellevate.Ai.Infrastructure.Data;
using Sellevate.BuildingBlocks.Eventing;
using Sellevate.BuildingBlocks.Idempotency;
using Sellevate.BuildingBlocks.Messaging;

namespace Sellevate.Ai.Eventing;

/// <summary>
/// Phase 40.19. Keeps <see cref="OrganizationProfileReplica"/> in step with the profile owned by
/// organization-service, so a persona prompt can be parameterized — and a banned claim enforced —
/// without leaving this service (docs/TENANCY/BACKGROUND_JOBS.md).
///
/// <para>
/// Runs in <b>tenant mode</b>: the profile lives inside a tenant, the envelope carries the
/// organization, and <see cref="KafkaConsumerBackgroundService.RequiresOrganization"/> stays at its
/// default <c>true</c>. A message without a tenant is dead-lettered rather than guessed at — which
/// matters more here than in learning, because guessing wrong would apply one customer's compliance
/// list to another's calls.
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

        var databaseContext = scopedServices.GetRequiredService<AiDbContext>();

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
