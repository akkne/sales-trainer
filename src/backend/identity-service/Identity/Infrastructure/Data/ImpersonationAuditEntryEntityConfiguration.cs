using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sellevate.Identity.Features.PlatformAdmin.Models;

namespace Sellevate.Identity.Infrastructure.Data;

public sealed class ImpersonationAuditEntryEntityConfiguration
    : IEntityTypeConfiguration<ImpersonationAuditEntry>
{
    private const int EmailMaximumLength = 320;
    private const int OrganizationNameMaximumLength = 200;
    private const int ReasonMaximumLength = 500;

    public void Configure(EntityTypeBuilder<ImpersonationAuditEntry> builder)
    {
        builder.ToTable("ImpersonationAuditEntries");
        builder.HasKey(entry => entry.Id);

        builder.Property(entry => entry.ActorUserId)
            .IsRequired();

        builder.Property(entry => entry.ActorEmail)
            .IsRequired()
            .HasMaxLength(EmailMaximumLength);

        builder.Property(entry => entry.OrganizationId)
            .IsRequired();

        builder.Property(entry => entry.OrganizationName)
            .IsRequired()
            .HasMaxLength(OrganizationNameMaximumLength);

        builder.Property(entry => entry.Reason)
            .IsRequired()
            .HasMaxLength(ReasonMaximumLength);

        builder.Property(entry => entry.IssuedAt)
            .IsRequired();

        builder.Property(entry => entry.ExpiresAt)
            .IsRequired();

        // The list endpoint is "who went in recently", newest first.
        builder.HasIndex(entry => entry.IssuedAt)
            .IsDescending();

        // The other question worth asking of this table: "who has been inside this organization".
        builder.HasIndex(entry => new { entry.OrganizationId, entry.IssuedAt })
            .IsDescending(false, true);
    }
}
