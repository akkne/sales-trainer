using Microsoft.Extensions.Options;
using Sellevate.BuildingBlocks.Eventing;
using Sellevate.BuildingBlocks.Idempotency;
using Sellevate.BuildingBlocks.Messaging;
using Sellevate.Learning.Identity;
using Sellevate.Learning.Infrastructure.Data;

namespace Sellevate.Learning.Eventing;

/// <summary>
/// Projects identity-service's user lifecycle onto <see cref="UserReplica"/>, so that learning-service
/// can name and picture a learner without a synchronous call to identity on every read.
///
/// <para>
/// The projection is <b>last-writer-wins per user id</b> and deliberately tolerant: an update or an
/// avatar change for a user this service has never seen is dropped rather than inserted, because the
/// registration event is the only one that carries the full record and inventing a half-filled replica
/// from an update would make the gap permanent. A redelivery therefore lands the same row.
/// </para>
/// </summary>
internal sealed class UserReplicaConsumer : KafkaConsumerBackgroundService
{
    public UserReplicaConsumer(
        IOptions<KafkaSettings> settings,
        IServiceScopeFactory scopeFactory,
        IIdempotencyStore idempotencyStore,
        ILogger<UserReplicaConsumer> logger)
        : base(settings, scopeFactory, idempotencyStore, logger)
    {
    }

    protected override IReadOnlyCollection<string> Topics =>
    [
        BuildingBlocks.Eventing.Topics.UserRegistered,
        BuildingBlocks.Eventing.Topics.UserUpdated,
        BuildingBlocks.Eventing.Topics.UserDeleted,
        BuildingBlocks.Eventing.Topics.UserAvatarChanged,
    ];

    /// <summary>
    /// Identity users are cross-org identities with no organization column
    /// (docs/TENANCY/TENANCY.md §4.2), so this replica projection is platform-global by design.
    /// </summary>
    protected override bool RequiresOrganization => false;

    protected override async Task HandleAsync(EventEnvelope envelope, IServiceProvider scopedServices, CancellationToken cancellationToken)
    {
        var databaseContext = scopedServices.GetRequiredService<LearningDbContext>();

        switch (envelope.Type)
        {
            case BuildingBlocks.Eventing.Topics.UserRegistered:
            {
                var payload = envelope.DataAs<UserRegisteredEvent>();
                if (payload is null)
                {
                    return;
                }

                var existing = await databaseContext.UserReplicas.FindAsync([payload.UserId], cancellationToken);
                if (existing is null)
                {
                    databaseContext.UserReplicas.Add(new UserReplica
                    {
                        UserId = payload.UserId,
                        Email = payload.Email,
                        DisplayName = payload.DisplayName,
                        AvatarKey = payload.AvatarKey,
                        UpdatedAt = DateTime.UtcNow,
                    });
                }
                else
                {
                    existing.Email = payload.Email;
                    existing.DisplayName = payload.DisplayName;
                    existing.AvatarKey = payload.AvatarKey;
                    existing.UpdatedAt = DateTime.UtcNow;
                }
                break;
            }

            case BuildingBlocks.Eventing.Topics.UserUpdated:
            {
                var payload = envelope.DataAs<UserUpdatedEvent>();
                if (payload is null)
                {
                    return;
                }

                var existing = await databaseContext.UserReplicas.FindAsync([payload.UserId], cancellationToken);
                if (existing is not null)
                {
                    existing.DisplayName = payload.DisplayName;
                    existing.AvatarKey = payload.AvatarKey;
                    existing.UpdatedAt = DateTime.UtcNow;
                }
                break;
            }

            case BuildingBlocks.Eventing.Topics.UserAvatarChanged:
            {
                var payload = envelope.DataAs<UserAvatarChangedEvent>();
                if (payload is null)
                {
                    return;
                }

                var existing = await databaseContext.UserReplicas.FindAsync([payload.UserId], cancellationToken);
                if (existing is not null)
                {
                    existing.AvatarKey = payload.AvatarKey;
                    existing.UpdatedAt = DateTime.UtcNow;
                }
                break;
            }

            case BuildingBlocks.Eventing.Topics.UserDeleted:
            {
                var payload = envelope.DataAs<UserDeletedEvent>();
                if (payload is null)
                {
                    return;
                }

                var existing = await databaseContext.UserReplicas.FindAsync([payload.UserId], cancellationToken);
                if (existing is not null)
                {
                    databaseContext.UserReplicas.Remove(existing);
                }
                break;
            }
        }

        await databaseContext.SaveChangesAsync(cancellationToken);
    }
}
