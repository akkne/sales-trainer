using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sellevate.Identity.Features.Avatars.Models;

namespace Sellevate.Identity.Infrastructure.Data;

public class DefaultAvatarEntityConfiguration : IEntityTypeConfiguration<DefaultAvatar>
{
    public void Configure(EntityTypeBuilder<DefaultAvatar> builder)
    {
        builder.ToTable("DefaultAvatars");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.ObjectKey).IsRequired();
        builder.Property(a => a.Index).IsRequired();

        builder.HasIndex(a => a.Index).IsUnique();
    }
}
