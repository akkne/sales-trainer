using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sellevate.Company.Common.Constants;
using Sellevate.Company.Features.Companies.Models;

namespace Sellevate.Company.Features.Companies.Configurations;

/// <summary>
/// Maps the <c>CompanyPersonas</c> table — saved buyer personas a salesperson practises against.
/// The difficulty is stored as its enum name rather than an ordinal, so reordering the enum cannot
/// silently reinterpret stored rows.
/// </summary>
internal sealed class CompanyPersonaEntityConfiguration : IEntityTypeConfiguration<CompanyPersona>
{
    public void Configure(EntityTypeBuilder<CompanyPersona> builder)
    {
        builder.ToTable("CompanyPersonas");

        builder.HasKey(persona => persona.Id);

        builder.Property(persona => persona.OrganizationId)
            .IsRequired();

        builder.Property(persona => persona.Name)
            .IsRequired()
            .HasMaxLength(CompanyFieldLengths.Name);

        builder.Property(persona => persona.Position)
            .IsRequired()
            .HasMaxLength(CompanyFieldLengths.Position);

        builder.Property(persona => persona.Personality)
            .IsRequired()
            .HasMaxLength(CompanyFieldLengths.PersonaPersonality);

        builder.Property(persona => persona.Difficulty)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(CompanyFieldLengths.PersonaDifficultyColumn)
            .HasDefaultValue(PersonaDifficulty.Medium);

        builder.Property(persona => persona.CreatedAt)
            .IsRequired();

        builder.HasIndex(persona => new { persona.OrganizationId, persona.CompanyId, persona.CreatedAt })
            .HasDatabaseName("IX_CompanyPersonas_OrganizationId_CompanyId_CreatedAt")
            .IsDescending(false, false, true);

        builder.HasIndex(persona => new { persona.OrganizationId, persona.UserId })
            .HasDatabaseName("IX_CompanyPersonas_OrganizationId_UserId");
    }
}
