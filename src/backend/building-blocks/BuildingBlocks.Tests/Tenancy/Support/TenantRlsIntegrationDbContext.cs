using Microsoft.EntityFrameworkCore;

namespace Sellevate.BuildingBlocks.Tests.Tenancy.Support;

internal sealed class TenantRlsIntegrationDbContext(DbContextOptions<TenantRlsIntegrationDbContext> options)
    : DbContext(options)
{
    public const string TableName = "TenancyRlsTestRows";

    public DbSet<TenantRlsTestRow> TenantRlsTestRows => Set<TenantRlsTestRow>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TenantRlsTestRow>().ToTable(TableName);
    }
}
