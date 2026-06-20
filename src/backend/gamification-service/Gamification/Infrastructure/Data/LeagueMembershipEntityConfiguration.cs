using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sellevate.Gamification.Features.League.Models;

namespace Sellevate.Gamification.Infrastructure.Data;

public sealed class LeagueMembershipEntityConfiguration : IEntityTypeConfiguration<LeagueMembership>
{
    public void Configure(EntityTypeBuilder<LeagueMembership> builder)
    {
        builder.ToTable("LeagueMemberships");
        builder.HasKey(membership => membership.Id);
        builder.HasIndex(membership => membership.LeagueId);
        builder.HasIndex(membership => new { membership.UserId, membership.LeagueId }).IsUnique();
    }
}
