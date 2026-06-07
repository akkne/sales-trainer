using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SalesTrainer.Api.Features.League.Models;

namespace SalesTrainer.Api.Infrastructure.Data;

public class LeagueSettingsConfiguration : IEntityTypeConfiguration<LeagueSettings>
{
    public void Configure(EntityTypeBuilder<LeagueSettings> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.MaximumLeagueParticipantCount).IsRequired();
        builder.Property(x => x.PromotionZoneSize).IsRequired();
        builder.Property(x => x.DemotionZoneSize).IsRequired();
    }
}
