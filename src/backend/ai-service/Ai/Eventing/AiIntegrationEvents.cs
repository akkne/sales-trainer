namespace Sellevate.Ai.Eventing;

public sealed record DialogEvaluatedEvent(
    Guid UserId,
    string SessionId,
    Guid BundleId,
    Guid ModeId,
    int RawScore,
    int XpEarned);

public sealed record GamificationDialogWeightsUpdatedEvent(
    int Confidence,
    int Structure,
    int Objection,
    int Goal,
    double Multiplier);

public sealed record UserRegisteredEvent(Guid UserId, string Email, string DisplayName, string? AvatarKey);

public sealed record UserUpdatedEvent(Guid UserId, string DisplayName, string? AvatarKey);

public sealed record UserDeletedEvent(Guid UserId);

/// <summary>
/// Phase 40.19. organization-service's <c>organization.profile.updated</c>, as this service needs
/// to read it. A local copy of the wire contract, matching every other incoming event here.
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
