using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sellevate.Identity.Features.Auth.Models;
using Sellevate.Identity.Features.Membership.Models;

namespace Sellevate.Identity.Infrastructure.Data;

public sealed class MembershipEntityConfiguration : IEntityTypeConfiguration<Membership>
{
    public void Configure(EntityTypeBuilder<Membership> builder)
    {
        builder.ToTable("Memberships");
        builder.HasKey(membership => new { membership.UserId, membership.OrganizationId });

        // Bare uuid, no FK — organization-service owns the registry (DB-per-service);
        // see docs/TENANCY/TENANCY.md §1.1.
        builder.Property(membership => membership.OrganizationId);

        builder.Property(membership => membership.Role)
            .HasConversion<int>();
        builder.Property(membership => membership.Status)
            .HasConversion<int>()
            .HasDefaultValue(MembershipStatus.Active);
        builder.Property(membership => membership.JoinedAt)
            .IsRequired();

        builder.HasIndex(membership => membership.OrganizationId);

        // UserId is same-service (this database also owns Users), so an FK is safe and
        // keeps the two tables consistent; unlike OrganizationId it is not a cross-service
        // reference.
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(membership => membership.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
