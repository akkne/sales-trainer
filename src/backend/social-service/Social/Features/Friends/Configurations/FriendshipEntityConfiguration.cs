using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sellevate.Social.Features.Friends.Models;

namespace Sellevate.Social.Features.Friends.Configurations;

public sealed class FriendshipEntityConfiguration : IEntityTypeConfiguration<Friendship>
{
    public void Configure(EntityTypeBuilder<Friendship> builder)
    {
        builder.ToTable("Friendships");

        builder.HasKey(friendship => friendship.Id);

        builder.Property(friendship => friendship.Status)
            .IsRequired();

        builder.Property(friendship => friendship.CreatedAt)
            .IsRequired();

        builder.HasIndex(friendship =>
            new { friendship.OrganizationId, friendship.RequesterId, friendship.AddresseeId })
            .IsUnique();

        // Canonical-pair index: ensures (A,B) and (B,A) cannot coexist even under concurrent inserts.
        // Stored as computed expression LEAST(id,id), GREATEST(id,id) at the DB level.
        //
        // Phase 40.13 put the organization first. Not cosmetic: memberships (40.6) let one person
        // belong to two customers, and the old platform-wide pair meant the second organization's
        // friendship between the same two people was rejected as a duplicate of the first.
        builder.HasIndex(friendship =>
            new { friendship.OrganizationId, friendship.CanonicalLowId, friendship.CanonicalHighId })
            .IsUnique()
            .HasDatabaseName("IX_Friendships_CanonicalPair");

        builder.Property(friendship => friendship.CanonicalLowId)
            .HasComputedColumnSql(
                "LEAST(\"RequesterId\", \"AddresseeId\")",
                stored: true);

        builder.Property(friendship => friendship.CanonicalHighId)
            .HasComputedColumnSql(
                "GREATEST(\"RequesterId\", \"AddresseeId\")",
                stored: true);

        builder.HasIndex(friendship => new { friendship.OrganizationId, friendship.RequesterId });
        builder.HasIndex(friendship => new { friendship.OrganizationId, friendship.AddresseeId });

        builder.ToTable(tableBuilder =>
            tableBuilder.HasCheckConstraint(
                "CK_Friendships_NoSelfFriendship",
                "\"RequesterId\" != \"AddresseeId\""));
    }
}
