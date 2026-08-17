using Microsoft.EntityFrameworkCore;
using Sellevate.Ai.Features.Dialog;
using Sellevate.Ai.Features.Organizations;
using Sellevate.Ai.Features.Dialog.Models;
using Sellevate.Ai.Identity;
using Sellevate.BuildingBlocks.Tenancy;

namespace Sellevate.Ai.Infrastructure.Data;

public sealed class AiDbContext : DbContext
{
    private readonly ITenantContext _tenantContext;

    public AiDbContext(DbContextOptions<AiDbContext> options, ITenantContext tenantContext)
        : base(options)
    {
        _tenantContext = tenantContext;
    }

    /// <summary>
    /// Phase 40.18. The tenant the query filters below are built from, exposed so that
    /// <c>DialogModeOverrideResolution</c> can decide whether to resolve overrides without every
    /// read service growing a constructor parameter for a value this context already holds.
    /// </summary>
    internal ITenantContext TenantContext => _tenantContext;

    public DbSet<DialogBundle> DialogBundles => Set<DialogBundle>();
    public DbSet<DialogMode> DialogModes => Set<DialogMode>();
    public DbSet<UserReplica> UserReplicas => Set<UserReplica>();
    public DbSet<OrganizationProfileReplica> OrganizationProfileReplicas => Set<OrganizationProfileReplica>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new DialogBundleConfiguration());
        modelBuilder.ApplyConfiguration(new DialogModeConfiguration());
        modelBuilder.ApplyConfiguration(new UserReplicaEntityConfiguration());
        modelBuilder.ApplyConfiguration(new OrganizationProfileReplicaEntityConfiguration());

        // Phase 40.11. Convenience, not security — the boundary is the RLS policy created by the
        // AddOrganizationId migration (docs/TENANCY/TENANCY.md 1.4). Both entities are listed
        // explicitly because EF query filters are NOT inherited through navigations: the filter on
        // DialogBundle says nothing about DialogMode even though every read composes
        // Bundle -> Modes. AiTenancyModelTests fails the build if an entity ever grows an
        // OrganizationId without appearing here.
        //
        // Both are content tables: null means the global dialog library shared by every
        // organization — the two seeded hidden bundles live there — so the comparison is
        // "mine or global", never plain equality (docs/TENANCY/CONTENT_MODEL.md).
        modelBuilder.Entity<DialogBundle>()
            .HasQueryFilter(bundle => _tenantContext.IsPlatformWide || bundle.OrganizationId == null || bundle.OrganizationId == _tenantContext.OrganizationId);
        modelBuilder.Entity<DialogMode>()
            .HasQueryFilter(mode => _tenantContext.IsPlatformWide || mode.OrganizationId == null || mode.OrganizationId == _tenantContext.OrganizationId);

        // Phase 40.19. The substitution profile is the one table here that is NOT content: there is
        // no global profile, and a null owner would mean every organization's banned claims applied
        // to everybody. Plain equality.
        modelBuilder.Entity<OrganizationProfileReplica>()
            .HasQueryFilter(replica => _tenantContext.IsPlatformWide || replica.OrganizationId == _tenantContext.OrganizationId);
    }
}
