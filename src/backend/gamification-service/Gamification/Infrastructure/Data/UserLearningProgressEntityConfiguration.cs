using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sellevate.Gamification.Features.Achievements.Models;

namespace Sellevate.Gamification.Infrastructure.Data;

public sealed class UserLearningProgressEntityConfiguration : IEntityTypeConfiguration<UserLearningProgress>
{
    public void Configure(EntityTypeBuilder<UserLearningProgress> builder)
    {
        builder.ToTable("UserLearningProgress");
        builder.HasKey(progress => progress.UserId);
    }
}
