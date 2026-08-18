using Microsoft.EntityFrameworkCore;
using Sellevate.BuildingBlocks.Tenancy;
using Sellevate.Company.Features.Companies.Configurations;
using Sellevate.Company.Features.Companies.Models;
using CompanyEntity = Sellevate.Company.Features.Companies.Models.Company;

namespace Sellevate.Company.Infrastructure.Data;

/// <summary>
/// The company-db context. All five entities are tenant-scoped — there is no global content in this
/// database — and each one carries its own organization query filter.
///
/// <para>
/// <b>The filters are convenience, not security.</b> The boundary is the row-level-security policy
/// the <c>AddOrganizationId</c> migration installs (docs/TENANCY/TENANCY.md §1.4-§1.5); these filters
/// only spare every call site from restating the organization.
/// </para>
///
/// <para>
/// <b>Only the organization half of the double scope lives here.</b> The user half stays an explicit
/// <c>UserId == userId</c> predicate on every query in <c>CompanyService</c>. A query filter cannot
/// express it — the user is a method argument, not ambient state — and hiding it in the model would
/// make "which rows can this caller see" impossible to read off a call site. The two halves fail in
/// different directions: the organization half leaks between customers, the user half leaks a
/// colleague's private pipeline inside one customer.
/// </para>
///
/// <para>
/// <b>Every tenant-scoped entity is listed one by one, on purpose.</b> EF does not inherit query
/// filters through navigations, so a filter on <c>Company</c> says nothing about the call logs,
/// practice calls, contacts and personas hanging off it. <c>CompanyTenancyModelTests</c> walks the
/// model and fails the build if an entity grows an <c>OrganizationId</c> without appearing here.
/// </para>
///
/// <para>
/// Registered with plain <c>AddDbContext</c> and never with the pooled variant: a pooled instance
/// would cache the first request's <c>ITenantContext</c>-backed filter and hand it to every later
/// caller (docs/CODESTYLE.md §6, <c>scripts/tenancy-pool-lint.py</c>). The registration also attaches
/// the two interceptors <c>AddSellevateTenancy</c> provides — the cross-tenant write guard, and the
/// one that issues <c>SET LOCAL app.organization_id</c> so the RLS policies have an organization to
/// compare against.
/// </para>
/// </summary>
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

        modelBuilder.Entity<CompanyEntity>()
            .HasQueryFilter(company => _tenantContext.IsPlatformWide || company.OrganizationId == _tenantContext.OrganizationId);
        modelBuilder.Entity<CallLogEntry>()
            .HasQueryFilter(entry => _tenantContext.IsPlatformWide || entry.OrganizationId == _tenantContext.OrganizationId);
        modelBuilder.Entity<PracticeCall>()
            .HasQueryFilter(practiceCall => _tenantContext.IsPlatformWide || practiceCall.OrganizationId == _tenantContext.OrganizationId);
        modelBuilder.Entity<CompanyContact>()
            .HasQueryFilter(contact => _tenantContext.IsPlatformWide || contact.OrganizationId == _tenantContext.OrganizationId);
        modelBuilder.Entity<CompanyPersona>()
            .HasQueryFilter(persona => _tenantContext.IsPlatformWide || persona.OrganizationId == _tenantContext.OrganizationId);
    }
}
