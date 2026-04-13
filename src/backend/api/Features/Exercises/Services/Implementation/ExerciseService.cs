using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SalesTrainer.Api.Features.Achievements.Services.Abstract;
using SalesTrainer.Api.Features.Exercises.Models;
using SalesTrainer.Api.Features.Exercises.Services.Abstract;
using SalesTrainer.Api.Features.Gamification.Models;
using SalesTrainer.Api.Features.Lessons.Models;
using SalesTrainer.Api.Infrastructure.Data;

namespace SalesTrainer.Api.Features.Exercises.Services.Implementation;

internal sealed class ExerciseService(
    AppDbContext databaseContext,
    ExerciseEvaluationFactory evaluationFactory,
    IAchievementService achievementService) : IExerciseService
{
    public async Task<IReadOnlyList<LessonSummaryDto>> GetAllLessonsAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var accessibleSkillIds = await databaseContext.UserSkillProgressRecords
            .Where(progressRecord => progressRecord.UserId == userId && progressRecord.Status != "locked")
            .Select(progressRecord => progressRecord.SkillId)
            .ToListAsync(cancellationToken);

        foreach (var skillId in accessibleSkillIds)
            await EnsureSkillLessonsSeededAsync(userId, skillId, cancellationToken);

        var lessonProgressByLessonId = await databaseContext.UserLessonProgressRecords
            .Where(progressRecord => progressRecord.UserId == userId)
            .ToDictionaryAsync(progressRecord => progressRecord.LessonId, cancellationToken);

        var allLessons = await databaseContext.Lessons
            .OrderBy(lesson => lesson.SortOrder)
            .ThenBy(lesson => lesson.Id)
            .ToListAsync(cancellationToken);

        return allLessons.Select(lesson =>
        {
            lessonProgressByLessonId.TryGetValue(lesson.Id, out var progressRecord);
            return new LessonSummaryDto(
                lesson.Id,
                lesson.Title,
                lesson.Description,
                lesson.SortOrder,
                lesson.DifficultyLevel,
                lesson.XpReward,
                lesson.EstimatedMinutes,
                progressRecord?.Status ?? "locked",
                progressRecord?.BestScore ?? 0);
        }).ToList();
    }

    public async Task<IReadOnlyList<LessonSummaryDto>> GetLessonsForSkillAsync(
        Guid userId,
        string skillSlug,
        CancellationToken cancellationToken = default)
    {
        var skill = await databaseContext.Skills
            .FirstOrDefaultAsync(skillRecord => skillRecord.Slug == skillSlug, cancellationToken)
            ?? throw new KeyNotFoundException($"Skill '{skillSlug}' not found.");

        await EnsureSkillLessonsSeededAsync(userId, skill.Id, cancellationToken);

        var lessonProgressByLessonId = await databaseContext.UserLessonProgressRecords
            .Where(progressRecord => progressRecord.UserId == userId)
            .ToDictionaryAsync(progressRecord => progressRecord.LessonId, cancellationToken);

        var allLessons = await databaseContext.Lessons
            .Where(lesson => lesson.SkillId == skill.Id)
            .OrderBy(lesson => lesson.SortOrder)
            .ToListAsync(cancellationToken);

        return allLessons.Select(lesson =>
        {
            lessonProgressByLessonId.TryGetValue(lesson.Id, out var progressRecord);
            return new LessonSummaryDto(
                lesson.Id,
                lesson.Title,
                lesson.Description,
                lesson.SortOrder,
                lesson.DifficultyLevel,
                lesson.XpReward,
                lesson.EstimatedMinutes,
                progressRecord?.Status ?? "locked",
                progressRecord?.BestScore ?? 0);
        }).ToList();
    }

    public async Task<IReadOnlyList<ExerciseDto>> GetExercisesForLessonAsync(
        Guid lessonId,
        CancellationToken cancellationToken = default)
    {
        var rawExercises = await databaseContext.Exercises
            .Where(exercise => exercise.LessonId == lessonId)
            .OrderBy(exercise => exercise.SortOrder)
            .Select(exercise => new { exercise.Id, exercise.Type, exercise.SortOrder, exercise.SerializedContent })
            .ToListAsync(cancellationToken);

        return rawExercises.Select(rawExercise => new ExerciseDto(
            rawExercise.Id,
            rawExercise.Type,
            rawExercise.SortOrder,
            JsonDocument.Parse(rawExercise.SerializedContent).RootElement))
            .ToList();
    }

    public async Task<ExerciseSubmissionResultDto> SubmitExerciseAnswerAsync(
        Guid userId,
        Guid exerciseId,
        JsonElement userAnswer,
        CancellationToken cancellationToken = default)
    {
        var exercise = await databaseContext.Exercises
            .FirstOrDefaultAsync(exerciseRecord => exerciseRecord.Id == exerciseId, cancellationToken)
            ?? throw new KeyNotFoundException($"Exercise {exerciseId} not found.");

        var evaluationStrategy = evaluationFactory.GetStrategyForExerciseType(exercise.Type);
        var exerciseContent = JsonDocument.Parse(exercise.SerializedContent).RootElement;

        var evaluationResult = await evaluationStrategy.EvaluateAnswerAsync(
            exerciseContent, userAnswer, cancellationToken);

        var newAttempt = new UserExerciseAttempt
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ExerciseId = exerciseId,
            SerializedAnswer = userAnswer.GetRawText(),
            IsCorrect = evaluationResult.IsCorrect,
            Score = evaluationResult.Score,
            SerializedAiFeedback = evaluationResult.AiFeedback is not null
                ? JsonSerializer.Serialize(new { feedback = evaluationResult.AiFeedback })
                : null,
            AttemptedAt = DateTime.UtcNow
        };

        databaseContext.UserExerciseAttempts.Add(newAttempt);

        var experiencePointsEarned = 0;
        if (evaluationResult.IsCorrect)
        {
            var parentLesson = await databaseContext.Lessons
                .FirstOrDefaultAsync(lesson => lesson.Id == exercise.LessonId, cancellationToken);

            experiencePointsEarned = parentLesson?.XpReward ?? 10;

            databaseContext.UserXpRecords.Add(new UserXp
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Amount = experiencePointsEarned,
                Source = "exercise",
                EarnedAt = DateTime.UtcNow
            });

            await UpdateLessonProgressAsync(userId, exercise.LessonId, cancellationToken);
            await UpdateStreakForUserAsync(userId, cancellationToken);
        }

        IReadOnlyList<string> newlyUnlockedAchievementKeys = Array.Empty<string>();
        if (evaluationResult.IsCorrect)
            newlyUnlockedAchievementKeys = await achievementService.EvaluateAchievementsAfterSubmitAsync(userId, cancellationToken);

        await databaseContext.SaveChangesAsync(cancellationToken);

        return new ExerciseSubmissionResultDto(
            evaluationResult.IsCorrect,
            evaluationResult.Score,
            evaluationResult.Explanation,
            evaluationResult.AiFeedback,
            experiencePointsEarned,
            newlyUnlockedAchievementKeys);
    }

    public async Task<NextLessonDto?> GetNextAvailableLessonAsync(
        Guid userId,
        Guid lessonId,
        CancellationToken cancellationToken = default)
    {
        var currentLesson = await databaseContext.Lessons
            .FirstOrDefaultAsync(lesson => lesson.Id == lessonId, cancellationToken);

        if (currentLesson is null) return null;

        var nextLesson = await databaseContext.Lessons
            .Where(lesson => lesson.SkillId == currentLesson.SkillId
                             && lesson.SortOrder > currentLesson.SortOrder)
            .OrderBy(lesson => lesson.SortOrder)
            .FirstOrDefaultAsync(cancellationToken);

        if (nextLesson is null) return null;

        var nextLessonProgress = await databaseContext.UserLessonProgressRecords
            .FirstOrDefaultAsync(progressRecord => progressRecord.UserId == userId
                                             && progressRecord.LessonId == nextLesson.Id, cancellationToken);

        if (nextLessonProgress is null || nextLessonProgress.Status == "locked")
            return null;

        return new NextLessonDto(nextLesson.Id, nextLesson.Title, nextLesson.XpReward);
    }

    private async Task UpdateLessonProgressAsync(
        Guid userId,
        Guid lessonId,
        CancellationToken cancellationToken = default)
    {
        var progressRecord = await databaseContext.UserLessonProgressRecords
            .FirstOrDefaultAsync(record => record.UserId == userId && record.LessonId == lessonId, cancellationToken);

        if (progressRecord is null)
        {
            progressRecord = new UserLessonProgress
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                LessonId = lessonId,
                Status = "completed",
                BestScore = 100,
                CompletedAt = DateTime.UtcNow
            };
            databaseContext.UserLessonProgressRecords.Add(progressRecord);
        }
        else if (progressRecord.Status != "completed")
        {
            progressRecord.Status = "completed";
            progressRecord.BestScore = 100;
            progressRecord.CompletedAt = DateTime.UtcNow;
        }

        var lesson = await databaseContext.Lessons
            .FirstOrDefaultAsync(lessonRecord => lessonRecord.Id == lessonId, cancellationToken);

        if (lesson is null) return;

        await UnlockNextLessonInSkillAsync(userId, lesson, cancellationToken);

        var skillProgressRecord = await databaseContext.UserSkillProgressRecords
            .FirstOrDefaultAsync(record => record.UserId == userId && record.SkillId == lesson.SkillId, cancellationToken);

        if (skillProgressRecord is null) return;

        var completedLessonCount = await databaseContext.UserLessonProgressRecords
            .CountAsync(record => record.UserId == userId
                             && record.LessonId != lessonId
                             && record.Status == "completed"
                             && databaseContext.Lessons
                                 .Where(lessonRecord => lessonRecord.Id == record.LessonId)
                                 .Select(lessonRecord => lessonRecord.SkillId)
                                 .Contains(lesson.SkillId), cancellationToken);

        skillProgressRecord.CompletedLessonCount = completedLessonCount + 1;

        if (skillProgressRecord.CompletedLessonCount >= skillProgressRecord.TotalLessonCount)
        {
            skillProgressRecord.Status = "completed";
            await UnlockNextSkillAsync(userId, lesson.SkillId, cancellationToken);
        }
        else
        {
            skillProgressRecord.Status = "in_progress";
        }
    }

    private async Task EnsureSkillLessonsSeededAsync(
        Guid userId,
        Guid skillId,
        CancellationToken cancellationToken = default)
    {
        var skillProgress = await databaseContext.UserSkillProgressRecords
            .AsNoTracking()
            .FirstOrDefaultAsync(record => record.UserId == userId && record.SkillId == skillId, cancellationToken);

        if (skillProgress is null || skillProgress.Status == "locked") return;

        var lessons = await databaseContext.Lessons
            .Where(lesson => lesson.SkillId == skillId)
            .OrderBy(lesson => lesson.SortOrder)
            .ThenBy(lesson => lesson.Id)
            .ToListAsync(cancellationToken);

        if (lessons.Count == 0) return;

        var lessonIds = lessons.Select(lesson => lesson.Id).ToList();
        var existingCount = await databaseContext.UserLessonProgressRecords
            .CountAsync(record => record.UserId == userId && lessonIds.Contains(record.LessonId), cancellationToken);

        if (existingCount > 0) return;

        for (var lessonIndex = 0; lessonIndex < lessons.Count; lessonIndex++)
        {
            databaseContext.UserLessonProgressRecords.Add(new UserLessonProgress
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                LessonId = lessons[lessonIndex].Id,
                Status = lessonIndex == 0 ? "available" : "locked",
                BestScore = 0
            });
        }

        await databaseContext.SaveChangesAsync(cancellationToken);
    }

    private async Task UnlockNextLessonInSkillAsync(
        Guid userId,
        Lesson completedLesson,
        CancellationToken cancellationToken = default)
    {
        var nextLesson = await databaseContext.Lessons
            .Where(lesson => lesson.SkillId == completedLesson.SkillId
                        && lesson.SortOrder > completedLesson.SortOrder)
            .OrderBy(lesson => lesson.SortOrder)
            .ThenBy(lesson => lesson.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (nextLesson is null) return;

        var nextProgress = await databaseContext.UserLessonProgressRecords
            .FirstOrDefaultAsync(record => record.UserId == userId && record.LessonId == nextLesson.Id, cancellationToken);

        if (nextProgress is null)
        {
            databaseContext.UserLessonProgressRecords.Add(new UserLessonProgress
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                LessonId = nextLesson.Id,
                Status = "available"
            });
        }
        else if (nextProgress.Status == "locked")
        {
            nextProgress.Status = "available";
        }
    }

    private async Task UnlockNextSkillAsync(
        Guid userId,
        Guid completedSkillId,
        CancellationToken cancellationToken = default)
    {
        var nextLockedSkillProgress = await databaseContext.UserSkillProgressRecords
            .Join(
                databaseContext.Skills,
                progressRecord => progressRecord.SkillId,
                skill => skill.Id,
                (progressRecord, skill) => new { progressRecord, skill })
            .Where(pair => pair.progressRecord.UserId == userId
                           && pair.progressRecord.Status == "locked"
                           && pair.skill.PrerequisiteSkillId == completedSkillId)
            .Select(pair => pair.progressRecord)
            .FirstOrDefaultAsync(cancellationToken);

        if (nextLockedSkillProgress is not null)
            nextLockedSkillProgress.Status = "available";
    }

    private async Task UpdateStreakForUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var streakRecord = await databaseContext.UserStreaks
            .FirstOrDefaultAsync(streak => streak.UserId == userId, cancellationToken);

        if (streakRecord is null)
        {
            databaseContext.UserStreaks.Add(new UserStreak
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                CurrentStreakDayCount = 1,
                LongestStreakDayCount = 1,
                LastActivityDate = today
            });
            AwardStreakBonusExperiencePointsIfMilestone(userId, 1);
            return;
        }

        if (streakRecord.LastActivityDate == today) return;

        var isConsecutiveDay = streakRecord.LastActivityDate == today.AddDays(-1);
        streakRecord.CurrentStreakDayCount = isConsecutiveDay
            ? streakRecord.CurrentStreakDayCount + 1
            : 1;

        if (streakRecord.CurrentStreakDayCount > streakRecord.LongestStreakDayCount)
            streakRecord.LongestStreakDayCount = streakRecord.CurrentStreakDayCount;

        streakRecord.LastActivityDate = today;

        AwardStreakBonusExperiencePointsIfMilestone(userId, streakRecord.CurrentStreakDayCount);
    }

    private void AwardStreakBonusExperiencePointsIfMilestone(Guid userId, int currentStreak)
    {
        int bonusExperiencePoints = currentStreak switch
        {
            7  => 50,
            30 => 200,
            _  => 0
        };

        if (bonusExperiencePoints == 0) return;

        databaseContext.UserXpRecords.Add(new UserXp
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Amount = bonusExperiencePoints,
            Source = "streak_bonus",
            EarnedAt = DateTime.UtcNow
        });
    }
}
