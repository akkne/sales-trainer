using Microsoft.EntityFrameworkCore;
using Sellevate.Company.Features.Companies.Configurations;
using Sellevate.Company.Features.Companies.Models;
using CompanyEntity = Sellevate.Company.Features.Companies.Models.Company;

namespace Sellevate.Company.Infrastructure.Data;

public sealed class CompanyDbContext : DbContext
{
    public CompanyDbContext(DbContextOptions<CompanyDbContext> options) : base(options)
    {
    }

    public DbSet<CompanyEntity> Companies => Set<CompanyEntity>();
    public DbSet<CallLogEntry> CallLogEntries => Set<CallLogEntry>();
    public DbSet<PracticeCall> PracticeCalls => Set<PracticeCall>();
    public DbSet<CompanyContact> CompanyContacts => Set<CompanyContact>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new CompanyEntityConfiguration());
        modelBuilder.ApplyConfiguration(new CallLogEntryEntityConfiguration());
        modelBuilder.ApplyConfiguration(new PracticeCallEntityConfiguration());
        modelBuilder.ApplyConfiguration(new CompanyContactEntityConfiguration());
    }
}
