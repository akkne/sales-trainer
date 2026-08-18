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
///
/// <para>
/// <b>The read and the write share one tenant transaction.</b> <c>SET LOCAL app.organization_id</c> is
/// issued by <c>TenantConnectionInterceptor</c> on <c>TransactionStarted</c>, so a bare
/// <c>FindAsync</c> outside a transaction runs with no tenant set: once RLS is actually enforced it
/// would return nothing, the handler would add a row that already exists, and <c>SaveChanges</c> would
/// fail the primary key — three retries, then the dead-letter queue. Every profile update after the
/// first would be lost while the neutral fallback wording made it look normal. Review, 40.34.
/// </para>
/// </summary>
internal sealed class OrganizationProfileConsumer : KafkaConsumerBackgroundService
{
    private const string EmptyJsonArray = "[]";
    private const string EmptyJsonObject = "{}";

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

        await using var tenantScope = await TenantTransactionScope.BeginWriteAsync(databaseContext, cancellationToken);

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
        existing.ObjectionsJson = CoalesceToDocumentDefault(payload.ObjectionsJson, EmptyJsonArray);
        existing.ScriptJson = CoalesceToDocumentDefault(payload.ScriptJson, EmptyJsonArray);
        existing.GlossaryJson = CoalesceToDocumentDefault(payload.GlossaryJson, EmptyJsonObject);
        existing.BannedClaimsJson = CoalesceToDocumentDefault(payload.BannedClaimsJson, EmptyJsonArray);
        existing.UpdatedAt = payload.UpdatedAt;

        await databaseContext.SaveChangesAsync(cancellationToken);
        await tenantScope.CommitAsync(cancellationToken);
    }

    /// <summary>
    /// The jsonb columns are <c>NOT NULL</c> here as they are at the source, and
    /// <c>System.Text.Json</c> will happily leave a non-nullable record property null when the field
    /// is absent from the message. Coalescing to an empty document keeps a truncated or
    /// older-shaped event from failing the whole projection.
    /// </summary>
    private static string CoalesceToDocumentDefault(string? value, string fallback)
        => string.IsNullOrWhiteSpace(value) ? fallback : value;
}
