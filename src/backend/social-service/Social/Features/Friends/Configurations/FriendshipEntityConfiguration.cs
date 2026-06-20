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

        builder.HasIndex(friendship => new { friendship.RequesterId, friendship.AddresseeId })
            .IsUnique();

        builder.HasIndex(friendship => friendship.RequesterId);
        builder.HasIndex(friendship => friendship.AddresseeId);

        builder.ToTable(tableBuilder =>
            tableBuilder.HasCheckConstraint(
                "CK_Friendships_NoSelfFriendship",
                "\"RequesterId\" != \"AddresseeId\""));
    }
}
