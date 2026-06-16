using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SalesTrainer.Api.Features.Gamification.Models;

namespace SalesTrainer.Api.Infrastructure.Data;

public sealed class GamificationSettingsEntityConfiguration : IEntityTypeConfiguration<GamificationSettings>
{
    public void Configure(EntityTypeBuilder<GamificationSettings> builder)
    {
        builder.HasKey(s => s.Id);
    }
}

public sealed class ExerciseTypeRewardEntityConfiguration : IEntityTypeConfiguration<ExerciseTypeReward>
{
    public void Configure(EntityTypeBuilder<ExerciseTypeReward> builder)
    {
        builder.HasKey(r => r.Id);
        builder.HasIndex(r => r.ExerciseType).IsUnique();
        builder.Property(r => r.ExerciseType).IsRequired().HasMaxLength(40);
    }
}

public sealed class StreakMilestoneEntityConfiguration : IEntityTypeConfiguration<StreakMilestone>
{
    public void Configure(EntityTypeBuilder<StreakMilestone> builder)
    {
        builder.HasKey(m => m.Id);
        builder.HasIndex(m => m.DayCount).IsUnique();
    }
}
