using Microsoft.EntityFrameworkCore;
using Sellevate.Learning.Features.DailyQuotes.Models;
using Sellevate.Learning.Features.Exercises.Models;
using Sellevate.Learning.Features.Lessons.Models;
using Sellevate.Learning.Features.Reference.Models;
using Sellevate.Learning.Features.SkillTree.Models;
using Sellevate.Learning.Features.Techniques.Models;
using Sellevate.Learning.Identity;

namespace Sellevate.Learning.Infrastructure.Data;

public sealed class LearningDbContext : DbContext
{
    public LearningDbContext(DbContextOptions<LearningDbContext> options) : base(options)
    {
    }

    public DbSet<Skill> Skills => Set<Skill>();
    public DbSet<SkillStage> SkillStages => Set<SkillStage>();
    public DbSet<Topic> Topics => Set<Topic>();
    public DbSet<UserSkillProgress> UserSkillProgressRecords => Set<UserSkillProgress>();
    public DbSet<Lesson> Lessons => Set<Lesson>();
    public DbSet<Exercise> Exercises => Set<Exercise>();
    public DbSet<UserLessonProgress> UserLessonProgressRecords => Set<UserLessonProgress>();
    public DbSet<UserExerciseAttempt> UserExerciseAttempts => Set<UserExerciseAttempt>();
    public DbSet<ExerciseTypePrompt> ExerciseTypePrompts => Set<ExerciseTypePrompt>();
    public DbSet<ReferenceMaterial> ReferenceMaterials => Set<ReferenceMaterial>();
    public DbSet<DailyQuote> DailyQuotes => Set<DailyQuote>();
    public DbSet<Technique> Techniques => Set<Technique>();
    public DbSet<TechniqueSkill> TechniqueSkills => Set<TechniqueSkill>();
    public DbSet<TechniqueCoach> TechniqueCoaches => Set<TechniqueCoach>();
    public DbSet<UserTechniqueProgress> UserTechniqueProgressRecords => Set<UserTechniqueProgress>();
    public DbSet<UserReplica> UserReplicas => Set<UserReplica>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(LearningDbContext).Assembly);
    }
}
