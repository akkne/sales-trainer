using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sellevate.Gamification.Features.Gamification.Models;

namespace Sellevate.Gamification.Infrastructure.Data;

public sealed class ExerciseTypeRewardEntityConfiguration : IEntityTypeConfiguration<ExerciseTypeReward>
{
    public void Configure(EntityTypeBuilder<ExerciseTypeReward> builder)
    {
        builder.ToTable("ExerciseTypeRewards");
        builder.HasKey(reward => reward.Id);
        builder.HasIndex(reward => reward.ExerciseType).IsUnique();
        builder.Property(reward => reward.ExerciseType).IsRequired().HasMaxLength(40);
    }
}
