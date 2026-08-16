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

        // One settings row per organization instead of one per installation. LeagueSettings is not
        // configuration but the state of a running competition — CurrentPeriodStartDate and
        // CurrentPeriodEndsAt say which week is currently open — and a shared row meant one
        // customer's admin pressing "close the league now" advanced every other customer's week.
        builder.HasIndex(settings => settings.OrganizationId).IsUnique();
    }
}
