using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SalesTrainer.Api.Features.Techniques.Models;

namespace SalesTrainer.Api.Features.Techniques;

public sealed class TechniqueCaseEntityConfiguration : IEntityTypeConfiguration<TechniqueCase>
{
    public void Configure(EntityTypeBuilder<TechniqueCase> builder)
    {
        builder.ToTable("TechniqueCases");

        builder.HasKey(techniqueCase => techniqueCase.Id);

        builder.Property(techniqueCase => techniqueCase.Title)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(techniqueCase => techniqueCase.Body)
            .IsRequired()
            .HasColumnType("text");

        builder.Property(techniqueCase => techniqueCase.MetricsJson)
            .HasColumnType("jsonb");

        builder.HasIndex(techniqueCase => new { techniqueCase.TechniqueId, techniqueCase.OrderIndex });
    }
}
