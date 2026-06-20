namespace Sellevate.Identity.Eventing;

/// <summary>
/// Payload contracts for the <c>user.*</c> integration events Identity produces onto
/// Kafka (see docs/MICROSERVICES.md §4.1). These are the public, versioned shapes other
/// services deserialize to seed/refresh their local <c>UserReplica</c>; keep them
/// additive-only so old consumers keep working.
/// </summary>
public sealed record UserRegisteredEvent(Guid UserId, string Email, string DisplayName, string? AvatarKey);

public sealed record UserUpdatedEvent(Guid UserId, string DisplayName, string? AvatarKey);

public sealed record UserDeletedEvent(Guid UserId);

public sealed record UserAvatarChangedEvent(Guid UserId, string? AvatarKey);
