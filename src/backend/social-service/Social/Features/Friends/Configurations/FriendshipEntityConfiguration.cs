using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sellevate.Social.Features.Friends.Models;

namespace Sellevate.Social.Features.Friends.Configurations;

/// <summary>
/// Maps the friendship row, and with it the two guarantees the application cannot enforce on its own.
///
/// <para>
/// <c>IX_Friendships_CanonicalPair</c> is the interesting one: the least/greatest pair of user ids is
/// a stored computed column, so (A,B) and (B,A) collide in one unique index and two people who ask
/// each other simultaneously cannot end up with two friendships. Phase 40.13 put the organization
/// first in that index — not cosmetic, since memberships (40.6) let one person belong to two
/// customers, and the old platform-wide pair rejected the second organization's friendship between
/// the same two people as a duplicate.
/// </para>
///
/// <para>
/// The check constraint refuses a self-friendship at the database, because a row that got past the
/// service would otherwise be unremovable through it.
/// </para>
/// </summary>
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
