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
