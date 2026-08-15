using Microsoft.EntityFrameworkCore;
using Sellevate.BuildingBlocks.Tenancy;
using Sellevate.Company.Features.Companies.Configurations;
using Sellevate.Company.Features.Companies.Models;
using CompanyEntity = Sellevate.Company.Features.Companies.Models.Company;

namespace Sellevate.Company.Infrastructure.Data;

public sealed class CompanyDbContext : DbContext
{
    private readonly ITenantContext _tenantContext;

    public CompanyDbContext(DbContextOptions<CompanyDbContext> options, ITenantContext tenantContext)
        : base(options)
    {
        _tenantContext = tenantContext;
    }

    public DbSet<CompanyEntity> Companies => Set<CompanyEntity>();
    public DbSet<CallLogEntry> CallLogEntries => Set<CallLogEntry>();
    public DbSet<PracticeCall> PracticeCalls => Set<PracticeCall>();
    public DbSet<CompanyContact> CompanyContacts => Set<CompanyContact>();
    public DbSet<CompanyPersona> CompanyPersonas => Set<CompanyPersona>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new CompanyEntityConfiguration());
        modelBuilder.ApplyConfiguration(new CallLogEntryEntityConfiguration());
        modelBuilder.ApplyConfiguration(new PracticeCallEntityConfiguration());
        modelBuilder.ApplyConfiguration(new CompanyContactEntityConfiguration());
        modelBuilder.ApplyConfiguration(new CompanyPersonaEntityConfiguration());

        // Phase 40.12. Convenience, not security — the boundary is the RLS policy the
        // AddOrganizationId migration installs (docs/TENANCY/TENANCY.md §1.4-§1.5).
        //
        // Only the ORGANIZATION half of company-service's double scope lives here. The USER half
        // stays an explicit `UserId == userId` predicate on every query in CompanyService: a query
        // filter cannot express it, because the user is a method argument rather than ambient
        // state, and hiding it in the model would make "which rows can this caller see" impossible
        // to read off a call site. Getting either half wrong is a bug in a different direction —
        // the organization half leaks between customers, the user half leaks a colleague's private
        // pipeline inside one customer.
        //
        // Every tenant-scoped entity is listed one by one: EF does NOT inherit query filters
        // through navigations, so a filter on Company says nothing about the CallLogEntries,
        // PracticeCalls, Contacts and Personas hanging off it. CompanyTenancyModelTests walks the
        // model and fails the build if an entity ever grows an OrganizationId without appearing
        // here.
        modelBuilder.Entity<CompanyEntity>()
            .HasQueryFilter(company => company.OrganizationId == _tenantContext.OrganizationId);
        modelBuilder.Entity<CallLogEntry>()
            .HasQueryFilter(entry => entry.OrganizationId == _tenantContext.OrganizationId);
        modelBuilder.Entity<PracticeCall>()
            .HasQueryFilter(practiceCall => practiceCall.OrganizationId == _tenantContext.OrganizationId);
        modelBuilder.Entity<CompanyContact>()
            .HasQueryFilter(contact => contact.OrganizationId == _tenantContext.OrganizationId);
        modelBuilder.Entity<CompanyPersona>()
            .HasQueryFilter(persona => persona.OrganizationId == _tenantContext.OrganizationId);
    }
}
