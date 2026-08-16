using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sellevate.Gamification.Features.League.Models;

namespace Sellevate.Gamification.Infrastructure.Data;

public sealed class LeagueEntityConfiguration : IEntityTypeConfiguration<League>
{
    public void Configure(EntityTypeBuilder<League> builder)
    {
        builder.ToTable("Leagues");
        builder.HasKey(league => league.Id);
        builder.Property(league => league.Tier).IsRequired();
        // Phase 40.13. This one is not an optimization: the old UNIQUE(WeekStartDate, Tier) says
        // "one bronze league per week for the entire platform", so the second customer to roll over
        // would have hit a unique violation and got no league at all. The organization has to be
        // part of the key, and that is why this index is replaced inside the migration rather than
        // deferred to the concurrent-rebuild script like the read indexes are.
        builder.HasIndex(league => new { league.OrganizationId, league.WeekStartDate, league.Tier }).IsUnique();
    }
}
