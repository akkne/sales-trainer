using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sellevate.Identity.Features.Auth.Models;

namespace Sellevate.Identity.Infrastructure.Data;

public class UserEntityConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");
        builder.HasKey(u => u.Id);
        builder.Property(u => u.AvatarType)
            .HasConversion<int>()
            .HasDefaultValue(AvatarKind.Default);
        builder.Property(u => u.DefaultAvatarIndex)
            .HasDefaultValue(0);
        builder.Property(u => u.IsEmailVerified)
            .HasDefaultValue(false);
    }
}
