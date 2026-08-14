using Microsoft.EntityFrameworkCore;
using Sellevate.BuildingBlocks.Tenancy;

namespace Sellevate.BuildingBlocks.Tests.Tenancy.Support;

internal sealed class TenantTestDbContext(DbContextOptions<TenantTestDbContext> options, ITenantContext tenantContext)
    : DbContext(options)
{
    public DbSet<TenantScopedTestEntity> TenantScopedTestEntities => Set<TenantScopedTestEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TenantScopedTestEntity>()
            .HasQueryFilter(entity => entity.OrganizationId == tenantContext.OrganizationId);
    }
}
