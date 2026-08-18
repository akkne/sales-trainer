using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sellevate.Identity.Features.Auth.Models;

namespace Sellevate.Identity.Infrastructure.Data;

/// <summary>
/// Maps the user table. The unique index on <c>Email</c> is the identity invariant of the whole service:
/// every login path resolves an address to at most one account, and a duplicate would make which account
/// answers depend on row order.
/// </summary>
public sealed class UserEntityConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");
        builder.HasKey(user => user.Id);
        builder.Property(user => user.AvatarType)
            .HasConversion<int>()
            .HasDefaultValue(AvatarKind.Default);
        builder.Property(user => user.DefaultAvatarIndex)
            .HasDefaultValue(0);
        builder.Property(user => user.IsEmailVerified)
            .HasDefaultValue(false);
        builder.HasIndex(user => user.Email).IsUnique();
    }
}
