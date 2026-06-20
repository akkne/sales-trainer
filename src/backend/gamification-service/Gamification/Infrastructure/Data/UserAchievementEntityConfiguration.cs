using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sellevate.Gamification.Features.Achievements.Models;

namespace Sellevate.Gamification.Infrastructure.Data;

public sealed class UserAchievementEntityConfiguration : IEntityTypeConfiguration<UserAchievement>
{
    public void Configure(EntityTypeBuilder<UserAchievement> builder)
    {
        builder.ToTable("UserAchievements");
        builder.HasKey(userAchievement => userAchievement.Id);
        builder.HasIndex(userAchievement => new { userAchievement.UserId, userAchievement.AchievementId }).IsUnique();
    }
}
