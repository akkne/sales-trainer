using System.Text.Json;
using SalesTrainer.Api.Features.Auth.Models;
using SalesTrainer.Api.Features.Discuss.Models;
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
        string iconicName = "cold-calling",
        string title = "Cold Calling",
        int orderInTree = 1)
    {
        var skill = new Skill
        {
            Id = Guid.NewGuid(),
            IconicName = iconicName,
            Title = title,
            OrderInTree = orderInTree,
            Description = "Test skill description"
        };
        db.Skills.Add(skill);
        await db.SaveChangesAsync();
        return skill;
    }

    public static async Task<Topic> SeedTopicAsync(
        AppDbContext db,
        Guid skillId,
        string? iconicName = null,
        string title = "Basics",
        int orderInSkill = 1)
    {
        var topic = new Topic
        {
            Id = Guid.NewGuid(),
            SkillId = skillId,
            IconicName = iconicName ?? $"topic-{Guid.NewGuid()}",
            Title = title,
            OrderInSkill = orderInSkill
        };
        db.Topics.Add(topic);
        await db.SaveChangesAsync();
        return topic;
    }

    public static async Task<Lesson> SeedLessonAsync(
        AppDbContext db,
        Guid topicId,
        string title = "Lesson 1",
        int orderInTopic = 1)
    {
        var lesson = new Lesson
        {
            Id = Guid.NewGuid(),
            TopicId = topicId,
            Title = title,
            OrderInTopic = orderInTopic
        };
        db.Lessons.Add(lesson);
        await db.SaveChangesAsync();
        return lesson;
    }

    public static async Task<Exercise> SeedMultipleChoiceExerciseAsync(
        AppDbContext db,
        Guid lessonId,
        int correctOptionIndex = 1,
        int orderInLesson = 1)
    {
        var options = new[]
        {
            new { text = "Option A", is_correct = correctOptionIndex == 0 },
            new { text = "Option B", is_correct = correctOptionIndex == 1 },
            new { text = "Option C", is_correct = correctOptionIndex == 2 }
        };

        var content = JsonSerializer.Serialize(new
        {
            situation = "What is the best approach?",
            options,
            explanation = "The correct option is the best because it works."
        });

        var exercise = new Exercise
        {
            Id = Guid.NewGuid(),
            LessonId = lessonId,
            Type = "choose_option",
            OrderInLesson = orderInLesson,
            SerializedContent = content,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
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

    public static async Task<DiscussTag> SeedDiscussTagAsync(
        AppDbContext db,
        string? slug = null,
        string? name = null,
        bool isCurated = true)
    {
        var resolvedSlug = slug ?? $"tag-{Guid.NewGuid()}";
        var tag = new DiscussTag
        {
            Id = Guid.NewGuid(),
            Slug = resolvedSlug,
            Name = name ?? resolvedSlug,
            IsCurated = isCurated,
            CreatedAt = DateTime.UtcNow
        };
        db.DiscussTags.Add(tag);
        await db.SaveChangesAsync();
        return tag;
    }

    public static async Task<DiscussThread> SeedDiscussThreadAsync(
        AppDbContext db,
        Guid authorId,
        string title = "How to handle objections?",
        string body = "Sharing my approach and looking for feedback.",
        int upvoteCount = 0,
        int replyCount = 0,
        bool isPinned = false,
        bool isHot = false,
        DateTime? createdAt = null,
        DateTime? lastActivityAt = null,
        Guid? tagId = null)
    {
        var now = createdAt ?? DateTime.UtcNow;
        var thread = new DiscussThread
        {
            Id = Guid.NewGuid(),
            AuthorId = authorId,
            Title = title,
            Body = body,
            UpvoteCount = upvoteCount,
            ReplyCount = replyCount,
            IsPinned = isPinned,
            IsHot = isHot,
            CreatedAt = now,
            UpdatedAt = now,
            LastActivityAt = lastActivityAt ?? now
        };
        if (tagId.HasValue)
        {
            thread.ThreadTags.Add(new DiscussThreadTag
            {
                Id = Guid.NewGuid(),
                ThreadId = thread.Id,
                TagId = tagId.Value
            });
        }
        db.DiscussThreads.Add(thread);
        await db.SaveChangesAsync();
        return thread;
    }

    public static async Task<DiscussReply> SeedDiscussReplyAsync(
        AppDbContext db,
        Guid threadId,
        Guid authorId,
        string body = "Great point, here is what worked for me.",
        int upvoteCount = 0)
    {
        var reply = new DiscussReply
        {
            Id = Guid.NewGuid(),
            ThreadId = threadId,
            AuthorId = authorId,
            Body = body,
            UpvoteCount = upvoteCount,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.DiscussReplies.Add(reply);
        await db.SaveChangesAsync();
        return reply;
    }

    public static async Task<DiscussVote> SeedDiscussVoteAsync(
        AppDbContext db,
        Guid userId,
        DiscussVoteTarget targetType,
        Guid targetId,
        DateTime? createdAt = null)
    {
        var vote = new DiscussVote
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TargetType = targetType,
            TargetId = targetId,
            CreatedAt = createdAt ?? DateTime.UtcNow
        };
        db.DiscussVotes.Add(vote);
        await db.SaveChangesAsync();
        return vote;
    }
}
