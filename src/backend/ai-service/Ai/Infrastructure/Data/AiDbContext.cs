using Microsoft.EntityFrameworkCore;
using Sellevate.Ai.Features.Dialog;
using Sellevate.Ai.Features.Organizations;
using Sellevate.Ai.Features.Quotas.Models;
using Sellevate.Ai.Features.Dialog.Models;
using Sellevate.Ai.Identity;
using Sellevate.BuildingBlocks.Tenancy;

namespace Sellevate.Ai.Infrastructure.Data;

/// <summary>
/// ai-service's relational store: the dialog content library, the replicated user and organization
/// profiles, and the quota ledger.
///
/// <para>
/// <b>Phase 40.11. The global query filters below are convenience, not security</b> — the boundary is
/// the RLS policy created by the <c>AddOrganizationId</c> migration (<c>docs/TENANCY/TENANCY.md</c>
/// §1.4). Every tenant-scoped entity must be listed explicitly, because EF query filters are <b>not</b>
/// inherited through navigations: the filter on <c>DialogBundle</c> says nothing about
/// <c>DialogMode</c> even though every read composes bundle → modes. <c>AiTenancyModelTests</c> fails
/// the build if an entity ever grows an <c>OrganizationId</c> without appearing here.
/// </para>
///
/// <para>
/// Two shapes of filter, and the difference matters. <c>DialogBundle</c> and <c>DialogMode</c> are
/// <i>content</i> tables: a null owner means the global dialog library shared by every organization —
/// the two seeded hidden bundles live there — so the comparison is "mine or global", never plain
/// equality (<c>docs/TENANCY/CONTENT_MODEL.md</c>). <c>OrganizationProfileReplica</c> (40.19) and both
/// quota tables (40.33) are strict tenant data and use plain equality: there is no global substitution
/// profile, no global allowance and no global bill, and a NULL owner there would mean one customer's
/// banned claims, limit or spend standing in for everybody's.
/// </para>
/// </summary>
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

    /// <summary>Phase 40.33. One row per organization that has been given limits of its own.</summary>
    public DbSet<OrganizationQuota> OrganizationQuotas => Set<OrganizationQuota>();

    /// <summary>Phase 40.33. What each organization spent on each model in each month.</summary>
    public DbSet<AiUsageRecord> AiUsageRecords => Set<AiUsageRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new DialogBundleConfiguration());
        modelBuilder.ApplyConfiguration(new DialogModeConfiguration());
        modelBuilder.ApplyConfiguration(new UserReplicaEntityConfiguration());
        modelBuilder.ApplyConfiguration(new OrganizationProfileReplicaEntityConfiguration());
        modelBuilder.ApplyConfiguration(new OrganizationQuotaEntityConfiguration());
        modelBuilder.ApplyConfiguration(new AiUsageRecordEntityConfiguration());

        modelBuilder.Entity<DialogBundle>()
            .HasQueryFilter(bundle => _tenantContext.IsPlatformWide || bundle.OrganizationId == null || bundle.OrganizationId == _tenantContext.OrganizationId);
        modelBuilder.Entity<DialogMode>()
            .HasQueryFilter(mode => _tenantContext.IsPlatformWide || mode.OrganizationId == null || mode.OrganizationId == _tenantContext.OrganizationId);

        modelBuilder.Entity<OrganizationProfileReplica>()
            .HasQueryFilter(replica => _tenantContext.IsPlatformWide || replica.OrganizationId == _tenantContext.OrganizationId);

        modelBuilder.Entity<OrganizationQuota>()
            .HasQueryFilter(quota => _tenantContext.IsPlatformWide || quota.OrganizationId == _tenantContext.OrganizationId);
        modelBuilder.Entity<AiUsageRecord>()
            .HasQueryFilter(record => _tenantContext.IsPlatformWide || record.OrganizationId == _tenantContext.OrganizationId);
    }
}
