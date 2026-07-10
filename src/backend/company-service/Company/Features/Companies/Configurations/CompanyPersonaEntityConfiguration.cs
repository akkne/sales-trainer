using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sellevate.Company.Features.Companies.Models;

namespace Sellevate.Company.Features.Companies.Configurations;

public sealed class CompanyPersonaEntityConfiguration : IEntityTypeConfiguration<CompanyPersona>
{
    public void Configure(EntityTypeBuilder<CompanyPersona> builder)
    {
        builder.ToTable("CompanyPersonas");

        builder.HasKey(persona => persona.Id);

        builder.Property(persona => persona.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(persona => persona.Position)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(persona => persona.Personality)
            .IsRequired()
            .HasMaxLength(4000);

        builder.Property(persona => persona.Difficulty)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(16)
            .HasDefaultValue(PersonaDifficulty.Medium);

        builder.Property(persona => persona.CreatedAt)
            .IsRequired();

        builder.HasIndex(persona => new { persona.CompanyId, persona.CreatedAt })
            .HasDatabaseName("IX_CompanyPersonas_CompanyId_CreatedAt")
            .IsDescending(false, true);
    }
}
