using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sellevate.Identity.Features.Auth.Models;

namespace Sellevate.Identity.Infrastructure.Data;

/// <summary>
/// Maps the per-organization login configuration. The organization identifier is the primary key
/// rather than a surrogate id, so a second configuration for the same organization cannot exist.
/// The GIN index on <c>AllowedEmailDomains</c> serves the first login step, which asks "which
/// organization claims this domain" and therefore cannot be selective by organization.
/// </summary>
public sealed class OrganizationAuthConfigurationEntityConfiguration
    : IEntityTypeConfiguration<OrganizationAuthConfiguration>
{
    private const int MethodMaximumLength = 32;

    public void Configure(EntityTypeBuilder<OrganizationAuthConfiguration> builder)
    {
        builder.ToTable("OrganizationAuthConfigurations");

        builder.HasKey(configuration => configuration.OrganizationId);
        builder.Property(configuration => configuration.OrganizationId)
            .ValueGeneratedNever();

        builder.Property(configuration => configuration.Method)
            .IsRequired()
            .HasMaxLength(MethodMaximumLength);

        builder.Property(configuration => configuration.ProviderSettings)
            .HasColumnType("jsonb");

        builder.Property(configuration => configuration.AllowedEmailDomains)
            .IsRequired()
            .HasColumnType("text[]");

        builder.Property(configuration => configuration.CreatedAt)
            .IsRequired();

        builder.HasIndex(configuration => configuration.AllowedEmailDomains)
            .HasMethod("gin");
    }
}
