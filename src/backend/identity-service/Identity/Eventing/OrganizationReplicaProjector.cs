using Sellevate.BuildingBlocks.Eventing;
using Sellevate.Identity.Features.Organizations.Models;
using Sellevate.Identity.Infrastructure.Data;

namespace Sellevate.Identity.Eventing;

/// <summary>
/// Turns one <c>organization.*</c> event into the corresponding <see cref="OrganizationReplica"/>
/// row. Split out of <see cref="OrganizationReplicaConsumer"/> so the projection can be exercised
/// without a Kafka broker, a Redis idempotency store or a running consume loop — the consumer is
/// left as the transport shell around it.
/// </summary>
internal static class OrganizationReplicaProjector
{
    public static async Task ApplyAsync(
        EventEnvelope envelope,
        IdentityDbContext databaseContext,
        CancellationToken cancellationToken)
    {
        switch (envelope.Type)
        {
            case Topics.OrganizationCreated:
            {
                if (envelope.DataAs<OrganizationCreatedEvent>() is not { } payload)
                {
                    return;
                }

                await UpsertAsync(
                    databaseContext,
                    payload.OrganizationId,
                    payload.Name,
                    payload.Slug,
                    OrganizationReplicaStatus.Active,
                    cancellationToken);
                break;
            }

            case Topics.OrganizationUpdated:
            {
                if (envelope.DataAs<OrganizationUpdatedEvent>() is not { } payload)
                {
                    return;
                }

                var status = Enum.TryParse<OrganizationReplicaStatus>(
                    payload.Status, ignoreCase: true, out var parsedStatus)
                    ? parsedStatus
                    : OrganizationReplicaStatus.Active;

                await UpsertAsync(
                    databaseContext,
                    payload.OrganizationId,
                    payload.Name,
                    payload.Slug,
                    status,
                    cancellationToken);
                break;
            }

            case Topics.OrganizationSuspended:
            {
                if (envelope.DataAs<OrganizationSuspendedEvent>() is not { } payload)
                {
                    return;
                }

                var existingReplica = await databaseContext.OrganizationReplicas
                    .FindAsync([payload.OrganizationId], cancellationToken);

                await UpsertAsync(
                    databaseContext,
                    payload.OrganizationId,
                    payload.Name,
                    existingReplica?.Slug ?? string.Empty,
                    OrganizationReplicaStatus.Suspended,
                    cancellationToken);
                break;
            }

            default:
                return;
        }

        await databaseContext.SaveChangesAsync(cancellationToken);
    }

    private static async Task UpsertAsync(
        IdentityDbContext databaseContext,
        Guid organizationId,
        string name,
        string slug,
        OrganizationReplicaStatus status,
        CancellationToken cancellationToken)
    {
        var existingReplica = await databaseContext.OrganizationReplicas
            .FindAsync([organizationId], cancellationToken);

        if (existingReplica is null)
        {
            databaseContext.OrganizationReplicas.Add(new OrganizationReplica
            {
                OrganizationId = organizationId,
                Name = name,
                Slug = slug,
                Status = status,
                UpdatedAt = DateTime.UtcNow,
            });
            return;
        }

        existingReplica.Name = name;
        existingReplica.Slug = slug;
        existingReplica.Status = status;
        existingReplica.UpdatedAt = DateTime.UtcNow;
    }
}
