using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sellevate.Gamification.Features.Gamification.Models;

namespace Sellevate.Gamification.Infrastructure.Data;

public sealed class GamificationSettingsEntityConfiguration : IEntityTypeConfiguration<GamificationSettings>
{
    public void Configure(EntityTypeBuilder<GamificationSettings> builder)
    {
        builder.ToTable("GamificationSettings");
        builder.HasKey(settings => settings.Id);
    }
}
