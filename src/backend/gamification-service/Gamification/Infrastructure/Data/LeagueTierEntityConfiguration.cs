using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sellevate.Gamification.Features.League.Models;

namespace Sellevate.Gamification.Infrastructure.Data;

public sealed class LeagueTierEntityConfiguration : IEntityTypeConfiguration<LeagueTier>
{
    public void Configure(EntityTypeBuilder<LeagueTier> builder)
    {
        builder.ToTable("LeagueTiers");
        builder.HasKey(tier => tier.Id);
        builder.Property(tier => tier.Key).IsRequired().HasMaxLength(40);
        builder.Property(tier => tier.Name).IsRequired().HasMaxLength(60);
        builder.Property(tier => tier.Color).IsRequired().HasMaxLength(20);
        builder.Property(tier => tier.Order).IsRequired();
        builder.HasIndex(tier => tier.Key).IsUnique();
    }
}
