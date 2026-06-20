using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sellevate.Gamification.Features.Gamification.Models;

namespace Sellevate.Gamification.Infrastructure.Data;

public sealed class StreakMilestoneEntityConfiguration : IEntityTypeConfiguration<StreakMilestone>
{
    public void Configure(EntityTypeBuilder<StreakMilestone> builder)
    {
        builder.ToTable("StreakMilestones");
        builder.HasKey(milestone => milestone.Id);
        builder.HasIndex(milestone => milestone.DayCount).IsUnique();
    }
}
