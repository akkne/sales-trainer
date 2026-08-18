using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sellevate.Gamification.Features.League.Models;

namespace Sellevate.Gamification.Infrastructure.Data;

/// <summary>
/// Phase 40.13. The unique index leads with the organization, and that is not an optimization: the
/// old <c>UNIQUE(WeekStartDate, Tier)</c> said "one bronze league per week for the entire platform",
/// so the second customer to roll over would have hit a unique violation and got no league at all.
/// The organization has to be part of the key, which is why this index is replaced inside the
/// migration rather than deferred to the concurrent-rebuild script like the read indexes are.
/// </summary>
public sealed class LeagueEntityConfiguration : IEntityTypeConfiguration<League>
{
    public void Configure(EntityTypeBuilder<League> builder)
    {
        builder.ToTable("Leagues");
        builder.HasKey(league => league.Id);
        builder.Property(league => league.Tier).IsRequired();
        builder.HasIndex(league => new { league.OrganizationId, league.WeekStartDate, league.Tier }).IsUnique();
    }
}
