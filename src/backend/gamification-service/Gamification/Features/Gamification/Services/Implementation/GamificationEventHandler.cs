using Sellevate.Gamification.Common.Constants;
using Sellevate.Gamification.Features.Achievements.Services.Abstract;
using Sellevate.Gamification.Features.Gamification.Services.Abstract;

namespace Sellevate.Gamification.Features.Gamification.Services.Implementation;

/// <summary>
/// Fans one learning or dialog event out into every gamification consequence it has, in a fixed order:
/// grant the points, register the day's activity, then re-evaluate achievements.
///
/// <para>
/// The order matters. Achievements are evaluated <b>last</b> because their conditions read the totals
/// and streak the two earlier steps have just written; evaluating first would delay every
/// points-threshold and streak-length badge by one event.
/// </para>
///
/// <para>
/// A grant only happens for a correct exercise or a positive dialog score, but the streak is registered
/// either way: showing up is the activity a streak measures, not succeeding.
/// </para>
/// </summary>
internal sealed class GamificationEventHandler(
    IExperiencePointsGrantService experiencePointsGrantService,
    IGamificationSettingsService settingsService,
    IStreakService streakService,
    IAchievementService achievementService,
    ILearningProgressService learningProgressService) : IGamificationEventHandler
{
    public async Task HandleExerciseCompletedAsync(
        Guid userId,
        string exerciseType,
        bool isCorrect,
        Guid? sourceEventId = null,
        CancellationToken cancellationToken = default)
    {
        if (isCorrect)
        {
            var baseExperiencePoints = await settingsService.GetExerciseBaseExperiencePointsAsync(exerciseType, cancellationToken);
            await experiencePointsGrantService.GrantAsync(
                userId, baseExperiencePoints, ExperiencePointsSources.Exercise, sourceEventId: sourceEventId, cancellationToken: cancellationToken);
        }

        await streakService.RegisterActivityAsync(userId, cancellationToken);
        await achievementService.EvaluateAchievementsAsync(userId, cancellationToken);
    }

    public async Task HandleDialogEvaluatedAsync(
        Guid userId,
        int experiencePointsEarned,
        Guid? sourceEventId = null,
        CancellationToken cancellationToken = default)
    {
        if (experiencePointsEarned > 0)
        {
            await experiencePointsGrantService.GrantAsync(
                userId, experiencePointsEarned, ExperiencePointsSources.Dialog, sourceEventId: sourceEventId, cancellationToken: cancellationToken);
        }

        await streakService.RegisterActivityAsync(userId, cancellationToken);
        await achievementService.EvaluateAchievementsAsync(userId, cancellationToken);
    }

    public async Task HandleLessonCompletedAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        await learningProgressService.RecordLessonCompletedAsync(userId, cancellationToken);
        await streakService.RegisterActivityAsync(userId, cancellationToken);
        await achievementService.EvaluateAchievementsAsync(userId, cancellationToken);
    }

    public async Task HandleSkillCompletedAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        await learningProgressService.RecordSkillCompletedAsync(userId, cancellationToken);
        await achievementService.EvaluateAchievementsAsync(userId, cancellationToken);
    }
}
