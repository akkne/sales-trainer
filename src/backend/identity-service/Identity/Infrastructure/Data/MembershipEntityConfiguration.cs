using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sellevate.Identity.Features.Auth.Models;
using Sellevate.Identity.Features.Membership.Models;

namespace Sellevate.Identity.Infrastructure.Data;

/// <summary>
/// Maps the membership join table. <c>OrganizationId</c> is a bare uuid with no foreign key on
/// purpose: organization-service owns the tenant registry under database-per-service, so this
/// column is a cross-service reference that cannot be enforced here (docs/TENANCY/TENANCY.md §1.1).
/// <c>UserId</c> is same-service — this database also owns Users — so it does carry a cascading
/// foreign key and keeps the two tables consistent.
/// </summary>
public sealed class MembershipEntityConfiguration : IEntityTypeConfiguration<Membership>
{
    public void Configure(EntityTypeBuilder<Membership> builder)
    {
        builder.ToTable("Memberships");
        builder.HasKey(membership => new { membership.UserId, membership.OrganizationId });

        builder.Property(membership => membership.OrganizationId);

        builder.Property(membership => membership.Role)
            .HasConversion<int>();
        builder.Property(membership => membership.Status)
            .HasConversion<int>()
            .HasDefaultValue(MembershipStatus.Active);
        builder.Property(membership => membership.JoinedAt)
            .IsRequired();

        builder.HasIndex(membership => membership.OrganizationId);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(membership => membership.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
