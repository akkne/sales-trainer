using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sellevate.Organization.Features.Organizations.Models;
using OrganizationEntity = Sellevate.Organization.Features.Organizations.Models.Organization;

namespace Sellevate.Organization.Features.Organizations.Configurations;

/// <summary>
/// The registry table's schema. <c>Id</c> is <c>ValueGeneratedNever</c> because the platform mints the
/// organization identifier itself and other services replicate it as a foreign key, so the database
/// must never invent one; <c>Slug</c> carries a unique index because it is a public URL segment and
/// <c>OrganizationService</c>'s pre-check turns a violation into a 409 rather than a 500;
/// <c>Status</c> is stored as its enum name so a value stays readable in the database after the enum
/// is reordered in code.
/// </summary>
public sealed class OrganizationEntityConfiguration : IEntityTypeConfiguration<OrganizationEntity>
{
    public void Configure(EntityTypeBuilder<OrganizationEntity> builder)
    {
        builder.ToTable("Organizations");

        builder.HasKey(organization => organization.Id);

        builder.Property(organization => organization.Id)
            .ValueGeneratedNever();

        builder.Property(organization => organization.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(organization => organization.Slug)
            .IsRequired()
            .HasMaxLength(120);

        builder.HasIndex(organization => organization.Slug)
            .IsUnique();

        builder.Property(organization => organization.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(32)
            .HasDefaultValue(OrganizationStatus.Active);

        builder.Property(organization => organization.CreatedAt)
            .IsRequired();

        builder.Property(organization => organization.UpdatedAt)
            .IsRequired();
    }
}
