namespace Sellevate.Learning.Eventing;

public sealed record UserRegisteredEvent(Guid UserId, string Email, string DisplayName, string? AvatarKey);

public sealed record UserUpdatedEvent(Guid UserId, string DisplayName, string? AvatarKey);

public sealed record UserDeletedEvent(Guid UserId);

public sealed record UserAvatarChangedEvent(Guid UserId, string? AvatarKey);

/// <summary>
/// Phase 40.19. organization-service's <c>organization.profile.updated</c>, as this service needs
/// to read it. A local copy of the contract rather than a shared assembly, matching the four events
/// above: services agree on the wire shape, not on a type.
/// </summary>
public sealed record OrganizationProfileUpdatedEvent(
    Guid OrganizationId,
    string? Product,
    string? Icp,
    string? Tone,
    string ObjectionsJson,
    string ScriptJson,
    string GlossaryJson,
    string BannedClaimsJson,
    DateTime UpdatedAt);
