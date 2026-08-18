using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sellevate.Identity.Features.Invites.Models;

namespace Sellevate.Identity.Infrastructure.Data;

/// <summary>
/// Maps the invite table. The <c>TokenHash</c> index is globally unique rather than per
/// organization because the acceptance path finds an invite by hash alone, before the caller has an
/// organization. The composite index puts <c>OrganizationId</c> first, matching the tenant-scoped
/// index rule in docs/TENANCY/TENANCY.md §3.
/// </summary>
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

        builder.HasIndex(invite => invite.TokenHash)
            .IsUnique();

        builder.HasIndex(invite => new { invite.OrganizationId, invite.Email });
    }
}
