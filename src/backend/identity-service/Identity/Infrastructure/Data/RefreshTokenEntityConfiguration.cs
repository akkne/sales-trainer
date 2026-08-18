using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sellevate.Identity.Features.Auth.Models;

namespace Sellevate.Identity.Infrastructure.Data;

/// <summary>
/// Maps the refresh tokens. <c>Token</c> holds the SHA-256 hash, never the raw token, and its unique
/// index is what makes reuse detection possible: rotation looks a token up by hash and a duplicate could
/// not be resolved to one row. Deleting a user cascades — a token outliving its owner is not a token.
/// </summary>
public sealed class RefreshTokenEntityConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("RefreshTokens");
        builder.HasKey(refreshToken => refreshToken.Id);
        builder.Property(refreshToken => refreshToken.Token).IsRequired();
        builder.HasIndex(refreshToken => refreshToken.Token).IsUnique();
        builder.HasOne(refreshToken => refreshToken.User)
            .WithMany()
            .HasForeignKey(refreshToken => refreshToken.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
