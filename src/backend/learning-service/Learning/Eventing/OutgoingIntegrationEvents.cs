namespace Sellevate.Learning.Eventing;

public sealed record ExerciseCompletedEvent(Guid UserId, string ExerciseType, int Score, bool IsCorrect);

public sealed record LessonCompletedEvent(Guid UserId, Guid LessonId, int BestScore);

public sealed record SkillCompletedEvent(Guid UserId, Guid SkillId);

public sealed record TechniqueMasteryChangedEvent(Guid UserId, Guid TechniqueId, int Level, int MasteryPercent);
