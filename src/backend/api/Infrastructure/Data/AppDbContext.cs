using Microsoft.EntityFrameworkCore;
using SalesTrainer.Api.Features.Achievements.Models;
using SalesTrainer.Api.Features.Auth.Models;
using SalesTrainer.Api.Features.Avatars.Models;
using SalesTrainer.Api.Features.DailyQuotes.Models;
using SalesTrainer.Api.Features.Dialog.Models;
using SalesTrainer.Api.Features.Discuss.Models;
using SalesTrainer.Api.Features.Exercises.Models;
using SalesTrainer.Api.Features.Friends.Models;
using SalesTrainer.Api.Features.Gamification.Models;
using SalesTrainer.Api.Features.League.Models;
using SalesTrainer.Api.Features.Lessons.Models;
using SalesTrainer.Api.Features.Notifications.Models;
using SalesTrainer.Api.Features.Onboarding.Models;
using SalesTrainer.Api.Features.Reference.Models;
using SalesTrainer.Api.Features.SkillTree.Models;
using SalesTrainer.Api.Features.Techniques.Models;

namespace SalesTrainer.Api.Infrastructure.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<UserProfile> UserProfiles => Set<UserProfile>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<EmailVerificationCode> EmailVerificationCodes => Set<EmailVerificationCode>();
    public DbSet<Skill> Skills => Set<Skill>();
    public DbSet<Topic> Topics => Set<Topic>();
    public DbSet<UserSkillProgress> UserSkillProgressRecords => Set<UserSkillProgress>();
    public DbSet<Lesson> Lessons => Set<Lesson>();
    public DbSet<Exercise> Exercises => Set<Exercise>();
    public DbSet<UserLessonProgress> UserLessonProgressRecords => Set<UserLessonProgress>();
    public DbSet<UserExerciseAttempt> UserExerciseAttempts => Set<UserExerciseAttempt>();
    public DbSet<UserStreak> UserStreaks => Set<UserStreak>();
    public DbSet<UserXp> UserXpRecords => Set<UserXp>();
    public DbSet<League> Leagues => Set<League>();
    public DbSet<LeagueMembership> LeagueMemberships => Set<LeagueMembership>();
    public DbSet<LeagueSettings> LeagueSettings => Set<LeagueSettings>();
    public DbSet<LeagueTier> LeagueTiers => Set<LeagueTier>();
    public DbSet<ReferenceMaterial> ReferenceMaterials => Set<ReferenceMaterial>();
    public DbSet<Achievement> Achievements => Set<Achievement>();
    public DbSet<UserAchievement> UserAchievements => Set<UserAchievement>();
    public DbSet<DialogBundle> DialogBundles => Set<DialogBundle>();
    public DbSet<DialogMode> DialogModes => Set<DialogMode>();
    public DbSet<OpenQuestionGlobalContext> OpenQuestionGlobalContexts => Set<OpenQuestionGlobalContext>();
    public DbSet<ExerciseTypePrompt> ExerciseTypePrompts => Set<ExerciseTypePrompt>();
    public DbSet<Friendship> Friendships => Set<Friendship>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<DailyQuote> DailyQuotes => Set<DailyQuote>();
    public DbSet<Technique> Techniques => Set<Technique>();
    public DbSet<TechniqueSkill> TechniqueSkills => Set<TechniqueSkill>();
    public DbSet<TechniqueCoach> TechniqueCoaches => Set<TechniqueCoach>();
    public DbSet<UserTechniqueProgress> UserTechniqueProgressRecords => Set<UserTechniqueProgress>();
    public DbSet<DiscussThread> DiscussThreads => Set<DiscussThread>();
    public DbSet<DiscussReply> DiscussReplies => Set<DiscussReply>();
    public DbSet<DiscussTag> DiscussTags => Set<DiscussTag>();
    public DbSet<DiscussThreadTag> DiscussThreadTags => Set<DiscussThreadTag>();
    public DbSet<DiscussVote> DiscussVotes => Set<DiscussVote>();
    public DbSet<DiscussPhoto> DiscussPhotos => Set<DiscussPhoto>();
    public DbSet<DefaultAvatar> DefaultAvatars => Set<DefaultAvatar>();
    public DbSet<GamificationSettings> GamificationSettings => Set<GamificationSettings>();
    public DbSet<ExerciseTypeReward> ExerciseTypeRewards => Set<ExerciseTypeReward>();
    public DbSet<StreakMilestone> StreakMilestones => Set<StreakMilestone>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
