using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sellevate.Organization.Features.Organizations.Models;

namespace Sellevate.Organization.Features.Organizations.Configurations;

public sealed class OrganizationProfileEntityConfiguration : IEntityTypeConfiguration<OrganizationProfile>
{
    public void Configure(EntityTypeBuilder<OrganizationProfile> builder)
    {
        builder.ToTable("OrganizationProfiles");

        builder.HasKey(profile => profile.OrganizationId);

        builder.Property(profile => profile.OrganizationId)
            .ValueGeneratedNever();

        builder.Property(profile => profile.Product)
            .HasColumnType("text");

        builder.Property(profile => profile.Icp)
            .HasColumnType("text");

        builder.Property(profile => profile.ObjectionsJson)
            .IsRequired()
            .HasColumnType("jsonb")
            .HasDefaultValue("[]");

        builder.Property(profile => profile.ScriptJson)
            .IsRequired()
            .HasColumnType("jsonb")
            .HasDefaultValue("[]");

        builder.Property(profile => profile.Tone)
            .HasColumnType("text");

        builder.Property(profile => profile.GlossaryJson)
            .IsRequired()
            .HasColumnType("jsonb")
            .HasDefaultValue("{}");

        builder.Property(profile => profile.BannedClaimsJson)
            .IsRequired()
            .HasColumnType("jsonb")
            .HasDefaultValue("[]");

        builder.Property(profile => profile.CreatedAt)
            .IsRequired();

        builder.Property(profile => profile.UpdatedAt)
            .IsRequired();
    }
}
