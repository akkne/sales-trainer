namespace Sellevate.Analytics.Features.Funnels.Models;

public sealed record UserRegisteredEvent(Guid UserId, string Email, string DisplayName, string? AvatarKey);

public sealed record ExerciseCompletedEvent(Guid UserId, Guid ExerciseId);

public sealed record ExperiencePointsGrantedEvent(Guid UserId, int Amount);
