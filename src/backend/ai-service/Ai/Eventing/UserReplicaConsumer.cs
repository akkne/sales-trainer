using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Sellevate.Ai.Identity;
using Sellevate.Ai.Infrastructure.Data;
using Sellevate.BuildingBlocks.Eventing;
using Sellevate.BuildingBlocks.Idempotency;
using Sellevate.BuildingBlocks.Messaging;

namespace Sellevate.Ai.Eventing;

/// <summary>
/// Projects identity-service's user events into <see cref="UserReplica"/>, so a transcript can name its
/// author without a cross-service call.
///
/// <para>
/// Runs with <c>RequiresOrganization</c> off: identity users are cross-organization identities with no
/// organization column (<c>docs/TENANCY/TENANCY.md</c> §4.2), so this projection is platform-global by
/// design and a message without a tenant is correct rather than suspect. Do not "fix" it — see
/// <c>docs/DECISIONS.md</c>.
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
        var databaseContext = scopedServices.GetRequiredService<AiDbContext>();

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
