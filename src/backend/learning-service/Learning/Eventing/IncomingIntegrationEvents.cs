namespace Sellevate.Learning.Eventing;

public sealed record UserRegisteredEvent(Guid UserId, string Email, string DisplayName, string? AvatarKey);

public sealed record UserUpdatedEvent(Guid UserId, string DisplayName, string? AvatarKey);

public sealed record UserDeletedEvent(Guid UserId);

public sealed record UserAvatarChangedEvent(Guid UserId, string? AvatarKey);
