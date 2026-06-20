using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sellevate.Gamification.Features.Achievements.Models;

namespace Sellevate.Gamification.Infrastructure.Data;

public sealed class AchievementEntityConfiguration : IEntityTypeConfiguration<Achievement>
{
    public void Configure(EntityTypeBuilder<Achievement> builder)
    {
        builder.ToTable("Achievements");
        builder.HasKey(achievement => achievement.Id);
        builder.Property(achievement => achievement.Key).IsRequired();
        builder.HasIndex(achievement => achievement.Key).IsUnique();
    }
}
