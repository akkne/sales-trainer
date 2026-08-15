using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Sellevate.BuildingBlocks.Tenancy;
using Sellevate.Company.Features.Companies.Models;
using Sellevate.Company.Infrastructure.Data;
using CompanyEntity = Sellevate.Company.Features.Companies.Models.Company;

namespace Sellevate.Company.Tests.Helpers;

internal static class TestCompanyDatabaseFactory
{
    /// <summary>
    /// Phase 40.12. The organization every unit-test row belongs to unless a test names another
    /// one. Company-service's scope is double — organization AND user — so a test fixture has to
    /// pin both halves, not just the user id it always pinned before.
    /// </summary>
    public static readonly Guid DefaultOrganizationId = Guid.Parse("0d9b8f8e-0000-4000-8000-000000000012");

    public static CompanyDbContext CreateInMemory(string? databaseName = null, Guid? organizationId = null)
        => CreateInMemory(BuildTenantContext(organizationId), databaseName);

    /// <summary>
    /// InMemory context for a caller that wants to control the tenant context itself — a different
    /// organization, or no organization at all (the "unset tenant reads nothing" case).
    /// </summary>
    public static CompanyDbContext CreateInMemory(ITenantContext tenantContext, string? databaseName = null)
    {
        var options = new DbContextOptionsBuilder<CompanyDbContext>()
            .UseInMemoryDatabase(databaseName ?? $"company-tests-{Guid.NewGuid()}")
            .EnableSensitiveDataLogging()
            // The real write guard, not a stand-in: it is what stamps OrganizationId on an entity
            // CompanyService creates without naming one, and what raises CrossTenantWriteException
            // on a foreign one. Leaving it out would make every unit test pass with Guid.Empty.
            .AddInterceptors(new TenantSaveChangesInterceptor(tenantContext))
            .Options;

        return new CompanyDbContext(options, tenantContext);
    }

    public static TenantContext BuildTenantContext(Guid? organizationId = null)
    {
        var tenantContext = new TenantContext();
        tenantContext.SetOrganization(organizationId ?? DefaultOrganizationId);
        return tenantContext;
    }

    /// <summary>
    /// Opens a second <see cref="CompanyDbContext"/> against the same named InMemory database as
    /// an existing context, with a <see cref="ISaveChangesInterceptor"/> attached. Used to
    /// deterministically simulate a real-database FK violation (the InMemory provider does not
    /// enforce foreign keys) for the ContactId concurrent-delete race — see
    /// CompanyServiceTests' *_translates_DbUpdateException_on_ContactId_fk_race_* tests.
    /// </summary>
    public static CompanyDbContext CreateInMemoryWithInterceptor(
        string databaseName,
        ISaveChangesInterceptor interceptor,
        Guid? organizationId = null)
    {
        var tenantContext = BuildTenantContext(organizationId);
        var options = new DbContextOptionsBuilder<CompanyDbContext>()
            .UseInMemoryDatabase(databaseName)
            .EnableSensitiveDataLogging()
            .AddInterceptors(new TenantSaveChangesInterceptor(tenantContext), interceptor)
            .Options;

        return new CompanyDbContext(options, tenantContext);
    }

    public static async Task<CompanyEntity> SeedCompanyAsync(
        CompanyDbContext databaseContext,
        Guid userId,
        string name,
        string description = "",
        CompanyStatus status = CompanyStatus.Lead,
        DateTime? nextActionAt = null,
        string? nextActionNote = null,
        DateTime? followUpNotifiedAt = null,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var company = new CompanyEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = name,
            Description = description,
            Status = status,
            NextActionAt = nextActionAt,
            NextActionNote = nextActionNote,
            FollowUpNotifiedAt = followUpNotifiedAt,
            CreatedAt = now,
            UpdatedAt = now
        };
        databaseContext.Companies.Add(company);
        await databaseContext.SaveChangesAsync(cancellationToken);
        return company;
    }

    public static async Task<PracticeCall> SeedPracticeCallAsync(
        CompanyDbContext databaseContext,
        Guid userId,
        Guid companyId,
        string goal,
        DateTime createdAt,
        CancellationToken cancellationToken = default)
    {
        var practiceCall = new PracticeCall
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            UserId = userId,
            DialogSessionId = Guid.NewGuid().ToString("N"),
            Goal = goal,
            CreatedAt = createdAt
        };
        databaseContext.PracticeCalls.Add(practiceCall);
        await databaseContext.SaveChangesAsync(cancellationToken);
        return practiceCall;
    }

    public static async Task<CompanyContact> SeedContactAsync(
        CompanyDbContext databaseContext,
        Guid userId,
        Guid companyId,
        string name,
        string position = "",
        string notes = "",
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var contact = new CompanyContact
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            UserId = userId,
            Name = name,
            Position = position,
            Notes = notes,
            CreatedAt = now,
            UpdatedAt = now
        };
        databaseContext.CompanyContacts.Add(contact);
        await databaseContext.SaveChangesAsync(cancellationToken);
        return contact;
    }
}
