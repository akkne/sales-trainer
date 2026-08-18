namespace Sellevate.Gamification.Features.Achievements.Services.Abstract;

/// <summary>
/// Keeps the counters achievement conditions are evaluated against — completed lessons, and whether
/// any skill has been finished — as a projection of learning-service's events.
///
/// <para>
/// Creates the user's progress row on first use, so no caller has to check whether one exists.
/// </para>
/// </summary>
public interface ILearningProgressService
{
    Task RecordLessonCompletedAsync(Guid userId, CancellationToken cancellationToken = default);

    Task RecordSkillCompletedAsync(Guid userId, CancellationToken cancellationToken = default);
}
