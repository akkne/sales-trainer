using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sellevate.Gamification.Features.League.Models;

namespace Sellevate.Gamification.Infrastructure.Data;

/// <summary>
/// One settings row per organization instead of one per installation. <c>LeagueSettings</c> is not
/// configuration but the state of a running competition — <c>CurrentPeriodStartDate</c> and
/// <c>CurrentPeriodEndsAt</c> say which week is currently open — and a shared row meant one
/// customer's admin pressing "close the league now" advanced every other customer's week.
/// </summary>
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

        builder.HasIndex(settings => settings.OrganizationId).IsUnique();
    }
}
