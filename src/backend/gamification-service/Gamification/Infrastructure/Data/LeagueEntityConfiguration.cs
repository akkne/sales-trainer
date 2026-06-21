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
        builder.HasIndex(league => new { league.WeekStartDate, league.Tier }).IsUnique();
    }
}
