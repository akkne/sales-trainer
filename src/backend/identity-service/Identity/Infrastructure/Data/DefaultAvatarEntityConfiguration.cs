using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sellevate.Identity.Features.Avatars.Models;

namespace Sellevate.Identity.Infrastructure.Data;

/// <summary>
/// Maps the stock avatar catalog. <c>Index</c> is uniquely indexed because it is the slot
/// <c>DefaultAvatarIndexResolver</c> maps a user onto: two rows claiming the same slot would make the
/// picture a user gets depend on row order.
/// </summary>
public sealed class DefaultAvatarEntityConfiguration : IEntityTypeConfiguration<DefaultAvatar>
{
    public void Configure(EntityTypeBuilder<DefaultAvatar> builder)
    {
        builder.ToTable("DefaultAvatars");
        builder.HasKey(defaultAvatar => defaultAvatar.Id);
        builder.Property(defaultAvatar => defaultAvatar.ObjectKey).IsRequired();
        builder.Property(defaultAvatar => defaultAvatar.Index).IsRequired();

        builder.HasIndex(defaultAvatar => defaultAvatar.Index).IsUnique();
    }
}
