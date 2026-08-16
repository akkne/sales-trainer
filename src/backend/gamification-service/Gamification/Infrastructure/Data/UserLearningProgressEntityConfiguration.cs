using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sellevate.Gamification.Features.Achievements.Models;

namespace Sellevate.Gamification.Infrastructure.Data;

public sealed class UserLearningProgressEntityConfiguration : IEntityTypeConfiguration<UserLearningProgress>
{
    public void Configure(EntityTypeBuilder<UserLearningProgress> builder)
    {
        builder.ToTable("UserLearningProgress");
        // Phase 40.13: the key becomes composite. A user id alone stopped identifying a row the
        // moment a person could belong to two customers (memberships, 40.6) — one of the two
        // organizations would have silently overwritten the other's counters.
        builder.HasKey(progress => new { progress.OrganizationId, progress.UserId });
    }
}
