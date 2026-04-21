using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SalesTrainer.Api.Features.Techniques.Models;

namespace SalesTrainer.Api.Features.Techniques;

public sealed class TechniqueCategoryEntityConfiguration : IEntityTypeConfiguration<TechniqueCategory>
{
    public void Configure(EntityTypeBuilder<TechniqueCategory> builder)
    {
        builder.ToTable("TechniqueCategories");

        builder.HasKey(category => category.Slug);

        builder.Property(category => category.Slug)
            .HasMaxLength(64);

        builder.Property(category => category.Label)
            .IsRequired()
            .HasMaxLength(120);

        builder.Property(category => category.Color)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(category => category.SortOrder)
            .IsRequired();
    }
}
