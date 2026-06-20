using Microsoft.EntityFrameworkCore;
using Sellevate.Identity.Features.Auth.Models;
using Sellevate.Identity.Features.Avatars.Models;
using Sellevate.Identity.Features.Onboarding.Models;

namespace Sellevate.Identity.Infrastructure.Data;

/// <summary>
/// The Identity service's own database context (Postgres <c>identity-db</c>). Owns the
/// identity bounded context only — Users, refresh tokens, email-verification codes,
/// user profiles and the default-avatar catalogue. No other service reaches into these
/// tables; they consume <c>user.*</c> Kafka events into a local <c>UserReplica</c> instead.
/// </summary>
public class IdentityDbContext(DbContextOptions<IdentityDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<EmailVerificationCode> EmailVerificationCodes => Set<EmailVerificationCode>();
    public DbSet<UserProfile> UserProfiles => Set<UserProfile>();
    public DbSet<DefaultAvatar> DefaultAvatars => Set<DefaultAvatar>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(IdentityDbContext).Assembly);
    }
}
