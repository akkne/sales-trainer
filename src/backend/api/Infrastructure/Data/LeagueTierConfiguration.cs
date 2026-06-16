using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SalesTrainer.Api.Features.League.Models;

namespace SalesTrainer.Api.Infrastructure.Data;

public class LeagueTierConfiguration : IEntityTypeConfiguration<LeagueTier>
{
    public void Configure(EntityTypeBuilder<LeagueTier> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Key).IsRequired().HasMaxLength(40);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(60);
        builder.Property(x => x.Color).IsRequired().HasMaxLength(20);
        builder.Property(x => x.Order).IsRequired();
        builder.HasIndex(x => x.Key).IsUnique();
    }
}
