using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SalesTrainer.Api.Features.Achievements;
using SalesTrainer.Api.Features.Gamification;
using SalesTrainer.Api.Features.Lessons;
using SalesTrainer.Api.Infrastructure.Data;

namespace SalesTrainer.Api.Features.Exercises;

public class ExerciseService(
    AppDbContext databaseContext,
    ExerciseEvaluationFactory evaluationFactory,
    AchievementService achievementService)
{
    public async Task<IReadOnlyList<LessonSummaryDto>> GetAllLessonsAsync(Guid userId)
    {
        // Lazy-init lesson progress for all accessible skills
        var accessibleSkillIds = await databaseContext.UserSkillProgressRecords
            .Where(p => p.UserId == userId && p.Status != "locked")
            .Select(p => p.SkillId)
            .ToListAsync();

        foreach (var skillId in accessibleSkillIds)
            await EnsureSkillLessonsSeededAsync(userId, skillId);

        var lessonProgressByLessonId = await databaseContext.UserLessonProgressRecords
            .Where(progress => progress.UserId == userId)
            .ToDictionaryAsync(progress => progress.LessonId);

        var allLessons = await databaseContext.Lessons
            .OrderBy(lesson => lesson.SortOrder)
            .ThenBy(lesson => lesson.Id)
            .ToListAsync();

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
        string skillSlug)
    {
        var skill = await databaseContext.Skills
            .FirstOrDefaultAsync(skill => skill.Slug == skillSlug)
            ?? throw new KeyNotFoundException($"Skill '{skillSlug}' not found.");

        // Lazy-init lesson progress for this skill on first access
        await EnsureSkillLessonsSeededAsync(userId, skill.Id);

        var lessonProgressByLessonId = await databaseContext.UserLessonProgressRecords
            .Where(progress => progress.UserId == userId)
            .ToDictionaryAsync(progress => progress.LessonId);

        var allLessons = await databaseContext.Lessons
            .Where(lesson => lesson.SkillId == skill.Id)
            .OrderBy(lesson => lesson.SortOrder)
            .ToListAsync();

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

    public async Task<IReadOnlyList<ExerciseDto>> GetExercisesForLessonAsync(Guid lessonId)
    {
        var rawExercises = await databaseContext.Exercises
            .Where(exercise => exercise.LessonId == lessonId)
            .OrderBy(exercise => exercise.SortOrder)
            .Select(exercise => new { exercise.Id, exercise.Type, exercise.SortOrder, exercise.SerializedContent })
            .ToListAsync();

        return rawExercises.Select(raw => new ExerciseDto(
            raw.Id,
            raw.Type,
            raw.SortOrder,
            JsonDocument.Parse(raw.SerializedContent).RootElement))
            .ToList();
    }

    public async Task<ExerciseSubmissionResultDto> SubmitExerciseAnswerAsync(
        Guid userId,
        Guid exerciseId,
        JsonElement userAnswer)
    {
        var exercise = await databaseContext.Exercises
            .FirstOrDefaultAsync(e => e.Id == exerciseId)
            ?? throw new KeyNotFoundException($"Exercise {exerciseId} not found.");

        var evaluationStrategy = evaluationFactory.GetStrategyForExerciseType(exercise.Type);
        var exerciseContent = JsonDocument.Parse(exercise.SerializedContent).RootElement;

        var evaluationResult = await evaluationStrategy.EvaluateAnswerAsync(
            exerciseContent, userAnswer);

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

        var xpEarned = 0;
        if (evaluationResult.IsCorrect)
        {
            var parentLesson = await databaseContext.Lessons
                .FirstOrDefaultAsync(lesson => lesson.Id == exercise.LessonId);

            xpEarned = parentLesson?.XpReward ?? 10;

            databaseContext.UserXpRecords.Add(new UserXp
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Amount = xpEarned,
                Source = "exercise",
                EarnedAt = DateTime.UtcNow
            });

            await UpdateLessonProgressAsync(userId, exercise.LessonId);
            await UpdateStreakForUserAsync(userId);
        }

        IReadOnlyList<string> newlyUnlockedAchievementKeys = Array.Empty<string>();
        if (evaluationResult.IsCorrect)
            newlyUnlockedAchievementKeys = await achievementService.EvaluateAchievementsAfterSubmitAsync(userId);

        await databaseContext.SaveChangesAsync();

        return new ExerciseSubmissionResultDto(
            evaluationResult.IsCorrect,
            evaluationResult.Score,
            evaluationResult.Explanation,
            evaluationResult.AiFeedback,
            xpEarned,
            newlyUnlockedAchievementKeys);
    }

    public async Task<NextLessonDto?> GetNextAvailableLessonAsync(Guid userId, Guid lessonId)
    {
        var currentLesson = await databaseContext.Lessons
            .FirstOrDefaultAsync(lesson => lesson.Id == lessonId);

        if (currentLesson is null) return null;

        var nextLesson = await databaseContext.Lessons
            .Where(lesson => lesson.SkillId == currentLesson.SkillId
                             && lesson.SortOrder > currentLesson.SortOrder)
            .OrderBy(lesson => lesson.SortOrder)
            .FirstOrDefaultAsync();

        if (nextLesson is null) return null;

        var nextLessonProgress = await databaseContext.UserLessonProgressRecords
            .FirstOrDefaultAsync(progress => progress.UserId == userId
                                             && progress.LessonId == nextLesson.Id);

        if (nextLessonProgress is null || nextLessonProgress.Status == "locked")
            return null;

        return new NextLessonDto(nextLesson.Id, nextLesson.Title, nextLesson.XpReward);
    }

    private async Task UpdateLessonProgressAsync(Guid userId, Guid lessonId)
    {
        var progressRecord = await databaseContext.UserLessonProgressRecords
            .FirstOrDefaultAsync(p => p.UserId == userId && p.LessonId == lessonId);

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
            .FirstOrDefaultAsync(l => l.Id == lessonId);

        if (lesson is null) return;

        // Unlock the next lesson in the same skill
        await UnlockNextLessonInSkillAsync(userId, lesson);

        var skillProgressRecord = await databaseContext.UserSkillProgressRecords
            .FirstOrDefaultAsync(p => p.UserId == userId && p.SkillId == lesson.SkillId);

        if (skillProgressRecord is null) return;

        var completedLessonCount = await databaseContext.UserLessonProgressRecords
            .CountAsync(p => p.UserId == userId
                             && p.LessonId != lessonId
                             && p.Status == "completed"
                             && databaseContext.Lessons
                                 .Where(l => l.Id == p.LessonId)
                                 .Select(l => l.SkillId)
                                 .Contains(lesson.SkillId));

        skillProgressRecord.CompletedLessonCount = completedLessonCount + 1;

        if (skillProgressRecord.CompletedLessonCount >= skillProgressRecord.TotalLessonCount)
        {
            skillProgressRecord.Status = "completed";
            await UnlockNextSkillAsync(userId, lesson.SkillId);
        }
        else
        {
            skillProgressRecord.Status = "in_progress";
        }
    }

    /// <summary>
    /// Seeds UserLessonProgress rows for a skill the first time it is accessed.
    /// First lesson → "available"; remaining → "locked".
    /// No-op if any progress rows already exist for this user+skill.
    /// </summary>
    private async Task EnsureSkillLessonsSeededAsync(Guid userId, Guid skillId)
    {
        var skillProgress = await databaseContext.UserSkillProgressRecords
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == userId && p.SkillId == skillId);

        if (skillProgress is null || skillProgress.Status == "locked") return;

        var lessons = await databaseContext.Lessons
            .Where(l => l.SkillId == skillId)
            .OrderBy(l => l.SortOrder)
            .ThenBy(l => l.Id)
            .ToListAsync();

        if (lessons.Count == 0) return;

        var lessonIds = lessons.Select(l => l.Id).ToList();
        var existingCount = await databaseContext.UserLessonProgressRecords
            .CountAsync(p => p.UserId == userId && lessonIds.Contains(p.LessonId));

        if (existingCount > 0) return;

        for (var i = 0; i < lessons.Count; i++)
        {
            databaseContext.UserLessonProgressRecords.Add(new UserLessonProgress
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                LessonId = lessons[i].Id,
                Status = i == 0 ? "available" : "locked",
                BestScore = 0
            });
        }

        await databaseContext.SaveChangesAsync();
    }

    /// <summary>
    /// After a lesson is completed, unlocks the next lesson in the same skill by sortOrder.
    /// </summary>
    private async Task UnlockNextLessonInSkillAsync(Guid userId, Lesson completedLesson)
    {
        var nextLesson = await databaseContext.Lessons
            .Where(l => l.SkillId == completedLesson.SkillId
                        && l.SortOrder > completedLesson.SortOrder)
            .OrderBy(l => l.SortOrder)
            .ThenBy(l => l.Id)
            .FirstOrDefaultAsync();

        if (nextLesson is null) return;

        var nextProgress = await databaseContext.UserLessonProgressRecords
            .FirstOrDefaultAsync(p => p.UserId == userId && p.LessonId == nextLesson.Id);

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

    private async Task UnlockNextSkillAsync(Guid userId, Guid completedSkillId)
    {
        var nextLockedSkillProgress = await databaseContext.UserSkillProgressRecords
            .Join(
                databaseContext.Skills,
                progress => progress.SkillId,
                skill => skill.Id,
                (progress, skill) => new { progress, skill })
            .Where(pair => pair.progress.UserId == userId
                           && pair.progress.Status == "locked"
                           && pair.skill.PrerequisiteSkillId == completedSkillId)
            .Select(pair => pair.progress)
            .FirstOrDefaultAsync();

        if (nextLockedSkillProgress is not null)
            nextLockedSkillProgress.Status = "available";
    }

    private async Task UpdateStreakForUserAsync(Guid userId)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var streakRecord = await databaseContext.UserStreaks
            .FirstOrDefaultAsync(streak => streak.UserId == userId);

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
            AwardStreakBonusXpIfMilestone(userId, 1);
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

        AwardStreakBonusXpIfMilestone(userId, streakRecord.CurrentStreakDayCount);
    }

    private void AwardStreakBonusXpIfMilestone(Guid userId, int currentStreak)
    {
        int bonusXp = currentStreak switch
        {
            7  => 50,
            30 => 200,
            _  => 0
        };

        if (bonusXp == 0) return;

        databaseContext.UserXpRecords.Add(new UserXp
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Amount = bonusXp,
            Source = "streak_bonus",
            EarnedAt = DateTime.UtcNow
        });
    }
}
