using System.Text.Json;
using SalesTrainer.Api.Features.Auth.Models;
using SalesTrainer.Api.Features.Exercises.Models;
using SalesTrainer.Api.Features.Lessons.Models;
using SalesTrainer.Api.Features.Onboarding.Models;
using SalesTrainer.Api.Features.SkillTree.Models;
using SalesTrainer.Api.Infrastructure.Data;

namespace SalesTrainer.Tests.Helpers;

public static class TestDbSeeder
{
    public static async Task<User> SeedUserAsync(
        AppDbContext db,
        string email = "test@example.com",
        string displayName = "Test User",
        UserRole role = UserRole.User)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password123!"),
            DisplayName = displayName,
            CreatedAt = DateTime.UtcNow,
            Role = role
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    public static async Task SeedOnboardingAsync(AppDbContext db, Guid userId)
    {
        db.UserProfiles.Add(new UserProfile
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            SalesType = "enterprise",
            ExperienceLevel = "beginner",
            Goal = "close more deals",
            IsOnboardingCompleted = true
        });
        await db.SaveChangesAsync();
    }

    public static async Task<Skill> SeedSkillAsync(
        AppDbContext db,
        string slug = "cold-calling",
        string title = "Cold Calling",
        int sortOrder = 1,
        Guid? prerequisiteSkillId = null,
        string[]? applicableSalesTypes = null)
    {
        var skill = new Skill
        {
            Id = Guid.NewGuid(),
            Slug = slug,
            Title = title,
            IconName = "phone",
            SortOrder = sortOrder,
            PrerequisiteSkillId = prerequisiteSkillId,
            ApplicableSalesTypes = applicableSalesTypes ?? ["enterprise", "smb"]
        };
        db.Skills.Add(skill);
        await db.SaveChangesAsync();
        return skill;
    }

    public static async Task<Lesson> SeedLessonAsync(
        AppDbContext db,
        Guid skillId,
        string title = "Lesson 1",
        int sortOrder = 1,
        int xpReward = 50)
    {
        var lesson = new Lesson
        {
            Id = Guid.NewGuid(),
            SkillId = skillId,
            Title = title,
            SortOrder = sortOrder,
            DifficultyLevel = 1,
            XpReward = xpReward
        };
        db.Lessons.Add(lesson);
        await db.SaveChangesAsync();
        return lesson;
    }

    public static async Task<Exercise> SeedMultipleChoiceExerciseAsync(
        AppDbContext db,
        Guid lessonId,
        int correctOptionIndex = 1,
        int sortOrder = 1)
    {
        var content = JsonSerializer.Serialize(new
        {
            question = "What is the best approach?",
            options = new[] { "Option A", "Option B", "Option C" },
            correctOptionIndex,
            explanation = "Option B is correct because it works."
        });

        var exercise = new Exercise
        {
            Id = Guid.NewGuid(),
            LessonId = lessonId,
            Type = "multiple_choice",
            SortOrder = sortOrder,
            SerializedContent = content
        };
        db.Exercises.Add(exercise);
        await db.SaveChangesAsync();
        return exercise;
    }

    public static async Task<UserSkillProgress> SeedSkillProgressAsync(
        AppDbContext db,
        Guid userId,
        Guid skillId,
        string status = "available",
        int totalLessonCount = 1)
    {
        var progress = new UserSkillProgress
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            SkillId = skillId,
            Status = status,
            CompletedLessonCount = 0,
            TotalLessonCount = totalLessonCount
        };
        db.UserSkillProgressRecords.Add(progress);
        await db.SaveChangesAsync();
        return progress;
    }
}
