using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sellevate.Gamification.Features.League.Models;

namespace Sellevate.Gamification.Infrastructure.Data;

public sealed class LeagueSettingsEntityConfiguration : IEntityTypeConfiguration<LeagueSettings>
{
    public void Configure(EntityTypeBuilder<LeagueSettings> builder)
    {
        builder.ToTable("LeagueSettings");
        builder.HasKey(settings => settings.Id);
        builder.Property(settings => settings.MaximumLeagueParticipantCount).IsRequired();
        builder.Property(settings => settings.PromotionZoneSize).IsRequired();
        builder.Property(settings => settings.DemotionZoneSize).IsRequired();
        builder.Property(settings => settings.PeriodLengthDays).IsRequired();
    }
}
