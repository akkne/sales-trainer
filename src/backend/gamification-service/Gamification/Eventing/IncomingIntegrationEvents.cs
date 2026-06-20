namespace Sellevate.Gamification.Eventing;

public sealed record UserRegisteredEvent(Guid UserId, string Email, string DisplayName, string? AvatarKey);

public sealed record UserUpdatedEvent(Guid UserId, string DisplayName, string? AvatarKey);

public sealed record UserDeletedEvent(Guid UserId);

public sealed record UserAvatarChangedEvent(Guid UserId, string? AvatarKey);

public sealed record ExerciseCompletedEvent(Guid UserId, string ExerciseType, int Score, bool IsCorrect);

public sealed record LessonCompletedEvent(Guid UserId, Guid LessonId, int BestScore);

public sealed record SkillCompletedEvent(Guid UserId, Guid SkillId);

public sealed record DialogEvaluatedEvent(
    Guid UserId,
    string SessionId,
    Guid BundleId,
    Guid ModeId,
    int RawScore,
    int XpEarned);
