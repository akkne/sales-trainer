using Microsoft.EntityFrameworkCore;
using Sellevate.BuildingBlocks.Tenancy;
using Sellevate.Organization.Features.Organizations.Configurations;
using Sellevate.Organization.Features.Organizations.Models;
using OrganizationEntity = Sellevate.Organization.Features.Organizations.Models.Organization;

namespace Sellevate.Organization.Infrastructure.Data;

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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new OrganizationEntityConfiguration());
        modelBuilder.ApplyConfiguration(new OrganizationProfileEntityConfiguration());

        modelBuilder.Entity<OrganizationProfile>()
            .HasQueryFilter(profile => _tenantContext.IsPlatformWide || profile.OrganizationId == _tenantContext.OrganizationId);
    }
}
