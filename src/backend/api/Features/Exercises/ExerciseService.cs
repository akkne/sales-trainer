using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SalesTrainer.Api.Features.Gamification;
using SalesTrainer.Api.Features.Lessons;
using SalesTrainer.Api.Infrastructure.Data;

namespace SalesTrainer.Api.Features.Exercises;

public class ExerciseService(
    AppDbContext databaseContext,
    ExerciseEvaluationFactory evaluationFactory)
{
    public async Task<IReadOnlyList<LessonSummaryDto>> GetLessonsForSkillAsync(
        Guid userId,
        string skillSlug)
    {
        var skill = await databaseContext.Skills
            .FirstOrDefaultAsync(skill => skill.Slug == skillSlug)
            ?? throw new KeyNotFoundException($"Skill '{skillSlug}' not found.");

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
                lesson.SortOrder,
                lesson.DifficultyLevel,
                lesson.XpReward,
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

        await databaseContext.SaveChangesAsync();

        return new ExerciseSubmissionResultDto(
            evaluationResult.IsCorrect,
            evaluationResult.Score,
            evaluationResult.Explanation,
            evaluationResult.AiFeedback,
            xpEarned);
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
