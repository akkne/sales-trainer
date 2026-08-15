using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sellevate.Identity.Features.Invites.Models;

namespace Sellevate.Identity.Infrastructure.Data;

public sealed class InviteEntityConfiguration : IEntityTypeConfiguration<Invite>
{
    private const int EmailMaximumLength = 320;
    private const int TokenHashLength = 64;

    public void Configure(EntityTypeBuilder<Invite> builder)
    {
        builder.ToTable("Invites");
        builder.HasKey(invite => invite.Id);

        builder.Property(invite => invite.OrganizationId)
            .IsRequired();

        builder.Property(invite => invite.Email)
            .IsRequired()
            .HasMaxLength(EmailMaximumLength);

        builder.Property(invite => invite.Role)
            .HasConversion<int>();

        builder.Property(invite => invite.TokenHash)
            .IsRequired()
            .HasMaxLength(TokenHashLength);

        builder.Property(invite => invite.ExpiresAt)
            .IsRequired();

        builder.Property(invite => invite.CreatedAt)
            .IsRequired();

        // The acceptance path finds the invite by hash alone, so the lookup must be unique and
        // indexed globally rather than per organization.
        builder.HasIndex(invite => invite.TokenHash)
            .IsUnique();

        // Organization first, matching the tenant-scoped index rule in docs/TENANCY/TENANCY.md §3.
        builder.HasIndex(invite => new { invite.OrganizationId, invite.Email });
    }
}
