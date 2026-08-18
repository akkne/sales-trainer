using Microsoft.Extensions.Options;
using Sellevate.Social.Identity;
using Sellevate.Social.Infrastructure.Data;
using Sellevate.BuildingBlocks.Eventing;
using Sellevate.BuildingBlocks.Idempotency;
using Sellevate.BuildingBlocks.Messaging;

namespace Sellevate.Social.Eventing;

/// <summary>
/// Keeps the local copy of the platform's user directory in step with identity-service, so that a
/// friend list or a thread can show a display name without a cross-service call on every read.
///
/// <para>
/// The projection is deliberately narrow — id, email, display name, avatar key — and last-write-wins:
/// handlers are idempotent, an update for a user this service has never seen is dropped rather than
/// inserted (only registration creates a row), and a delete removes the row outright. It is eventually
/// consistent by construction, which is why every reader has a fallback display name.
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
    ];

    /// <summary>
    /// Identity users are cross-org identities with no organization column
    /// (docs/TENANCY/TENANCY.md §4.2), so this replica projection is platform-global by design.
    /// </summary>
    protected override bool RequiresOrganization => false;

    protected override async Task HandleAsync(EventEnvelope envelope, IServiceProvider scopedServices, CancellationToken cancellationToken)
    {
        var databaseContext = scopedServices.GetRequiredService<SocialDbContext>();

        switch (envelope.Type)
        {
            case BuildingBlocks.Eventing.Topics.UserRegistered:
            {
                var payload = envelope.DataAs<UserRegisteredEvent>();
                if (payload is null) return;

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
                if (payload is null) return;

                var existing = await databaseContext.UserReplicas.FindAsync([payload.UserId], cancellationToken);
                if (existing is not null)
                {
                    existing.DisplayName = payload.DisplayName;
                    existing.AvatarKey = payload.AvatarKey;
                    existing.UpdatedAt = DateTime.UtcNow;
                }
                break;
            }

            case BuildingBlocks.Eventing.Topics.UserDeleted:
            {
                var payload = envelope.DataAs<UserDeletedEvent>();
                if (payload is null) return;

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
