using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sellevate.Identity.Features.Auth.Models;

namespace Sellevate.Identity.Infrastructure.Data;

public sealed class OrganizationAuthConfigurationEntityConfiguration
    : IEntityTypeConfiguration<OrganizationAuthConfiguration>
{
    private const int MethodMaximumLength = 32;

    public void Configure(EntityTypeBuilder<OrganizationAuthConfiguration> builder)
    {
        builder.ToTable("OrganizationAuthConfigurations");

        // The organization is the key: one login configuration per organization, no surrogate id
        // to get out of sync with it.
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

        // The domain lookup on the first login step is the hot path and is not selective by
        // organization — it asks "which organization claims this domain".
        builder.HasIndex(configuration => configuration.AllowedEmailDomains)
            .HasMethod("gin");
    }
}
