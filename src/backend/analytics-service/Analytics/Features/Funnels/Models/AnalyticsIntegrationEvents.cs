namespace Sellevate.Analytics.Features.Funnels.Models;

public sealed record UserRegisteredEvent(Guid UserId, string Email, string DisplayName, string? AvatarKey);

public sealed record ExerciseCompletedEvent(Guid UserId, string ExerciseType, int Score, bool IsCorrect);

public sealed record ExperiencePointsGrantedEvent(Guid UserId, int Amount, string Source);
