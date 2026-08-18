using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sellevate.Gamification.Features.Gamification.Models;

namespace Sellevate.Gamification.Infrastructure.Data;

/// <summary>
/// Phase 40.13: the organization leads, and the uniqueness is per organization. The old global
/// <c>UNIQUE(UserId)</c> would have refused a second streak for a person who belongs to two
/// customers — which memberships (40.6) make possible.
/// </summary>
public sealed class UserStreakEntityConfiguration : IEntityTypeConfiguration<UserStreak>
{
    public void Configure(EntityTypeBuilder<UserStreak> builder)
    {
        builder.ToTable("UserStreaks");
        builder.HasKey(streak => streak.Id);
        builder.HasIndex(streak => new { streak.OrganizationId, streak.UserId }).IsUnique();
    }
}
