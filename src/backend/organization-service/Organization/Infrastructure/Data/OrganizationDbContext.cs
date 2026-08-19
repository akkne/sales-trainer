using Microsoft.EntityFrameworkCore;
using Sellevate.BuildingBlocks.Tenancy;
using Sellevate.Organization.Features.DemoRequests.Configurations;
using Sellevate.Organization.Features.DemoRequests.Models;
using Sellevate.Organization.Features.Organizations.Configurations;
using Sellevate.Organization.Features.Organizations.Models;
using DemoRequestEntity = Sellevate.Organization.Features.DemoRequests.Models.DemoRequest;
using OrganizationEntity = Sellevate.Organization.Features.Organizations.Models.Organization;

namespace Sellevate.Organization.Infrastructure.Data;

/// <summary>
/// The service's three tables, and the asymmetry between them that the whole tenancy story rests on:
/// <see cref="Organizations"/> is the tenant registry and <see cref="DemoRequests"/> is a lead that
/// precedes any tenant, so neither carries a query filter, while <see cref="OrganizationProfiles"/> is
/// tenant-scoped and is filtered on every read (docs/TENANCY/TENANCY.md §1.2, §1.9).
///
/// <para>
/// <b>Must be registered with <c>AddDbContext</c>, never <c>AddDbContextPool</c>.</b> The filter below
/// closes over the request-scoped <c>ITenantContext</c> at model-build time; a pooled instance would
/// carry the first tenant it saw into every later request (CODESTYLE §6, docs/TENANCY/TENANCY.md §1.4).
/// </para>
/// </summary>
public sealed class OrganizationDbContext : DbContext
{
    private readonly ITenantContext _tenantContext;

    public OrganizationDbContext(DbContextOptions<OrganizationDbContext> options, ITenantContext tenantContext)
        : base(options)
    {
        _tenantContext = tenantContext;
    }

    public DbSet<OrganizationEntity> Organizations => Set<OrganizationEntity>();

    public DbSet<OrganizationProfile> OrganizationProfiles => Set<OrganizationProfile>();

    public DbSet<DemoRequestEntity> DemoRequests => Set<DemoRequestEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new OrganizationEntityConfiguration());
        modelBuilder.ApplyConfiguration(new OrganizationProfileEntityConfiguration());
        modelBuilder.ApplyConfiguration(new DemoRequestEntityConfiguration());

        modelBuilder.Entity<OrganizationProfile>()
            .HasQueryFilter(profile => _tenantContext.IsPlatformWide || profile.OrganizationId == _tenantContext.OrganizationId);
    }
}
