using Microsoft.EntityFrameworkCore;
using Sellevate.BuildingBlocks.Outbox;
using Sellevate.BuildingBlocks.Tenancy;
using Sellevate.Identity.Features.Auth.Models;
using Sellevate.Identity.Features.Avatars.Models;
using Sellevate.Identity.Features.Invites.Models;
using Sellevate.Identity.Features.Membership.Models;
using Sellevate.Identity.Features.Onboarding.Models;

namespace Sellevate.Identity.Infrastructure.Data;

public class IdentityDbContext(DbContextOptions<IdentityDbContext> options, ITenantContext tenantContext)
    : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<EmailVerificationCode> EmailVerificationCodes => Set<EmailVerificationCode>();
    public DbSet<UserProfile> UserProfiles => Set<UserProfile>();
    public DbSet<DefaultAvatar> DefaultAvatars => Set<DefaultAvatar>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<Membership> Memberships => Set<Membership>();
    public DbSet<Invite> Invites => Set<Invite>();

    /// <summary>
    /// Phase 40.8. No query filter and no row-level security on purpose — it is read before
    /// authentication, when no tenant context exists. See
    /// <see cref="OrganizationAuthConfiguration"/>.
    /// </summary>
    public DbSet<OrganizationAuthConfiguration> OrganizationAuthConfigurations
        => Set<OrganizationAuthConfiguration>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(IdentityDbContext).Assembly);

        // Convenience, not security — the security boundary is the RLS policy created by the
        // AddInvite migration (docs/TENANCY/TENANCY.md §1.4).
        modelBuilder.Entity<Invite>()
            .HasQueryFilter(invite => invite.OrganizationId == tenantContext.OrganizationId);
    }
}
