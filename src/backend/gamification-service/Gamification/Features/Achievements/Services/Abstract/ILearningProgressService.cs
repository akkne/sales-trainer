namespace Sellevate.Gamification.Features.Achievements.Services.Abstract;

public interface ILearningProgressService
{
    Task RecordLessonCompletedAsync(Guid userId, CancellationToken cancellationToken = default);

    Task RecordSkillCompletedAsync(Guid userId, CancellationToken cancellationToken = default);
}
