using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Sellevate.Company.Features.Companies.Models;
using Sellevate.Company.Infrastructure.Data;
using CompanyEntity = Sellevate.Company.Features.Companies.Models.Company;

namespace Sellevate.Company.Tests.Helpers;

internal static class TestCompanyDatabaseFactory
{
    public static CompanyDbContext CreateInMemory(string? databaseName = null)
    {
        var options = new DbContextOptionsBuilder<CompanyDbContext>()
            .UseInMemoryDatabase(databaseName ?? $"company-tests-{Guid.NewGuid()}")
            .EnableSensitiveDataLogging()
            .Options;

        return new CompanyDbContext(options);
    }

    /// <summary>
    /// Opens a second <see cref="CompanyDbContext"/> against the same named InMemory database as
    /// an existing context, with a <see cref="ISaveChangesInterceptor"/> attached. Used to
    /// deterministically simulate a real-database FK violation (the InMemory provider does not
    /// enforce foreign keys) for the ContactId concurrent-delete race — see
    /// CompanyServiceTests' *_translates_DbUpdateException_on_ContactId_fk_race_* tests.
    /// </summary>
    public static CompanyDbContext CreateInMemoryWithInterceptor(string databaseName, ISaveChangesInterceptor interceptor)
    {
        var options = new DbContextOptionsBuilder<CompanyDbContext>()
            .UseInMemoryDatabase(databaseName)
            .EnableSensitiveDataLogging()
            .AddInterceptors(interceptor)
            .Options;

        return new CompanyDbContext(options);
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
