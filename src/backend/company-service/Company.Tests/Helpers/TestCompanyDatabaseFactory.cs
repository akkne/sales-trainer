using Microsoft.EntityFrameworkCore;
using Sellevate.Company.Infrastructure.Data;
using CompanyEntity = Sellevate.Company.Features.Companies.Models.Company;

namespace Sellevate.Company.Tests.Helpers;

internal static class TestCompanyDatabaseFactory
{
    public static CompanyDbContext CreateInMemory()
    {
        var options = new DbContextOptionsBuilder<CompanyDbContext>()
            .UseInMemoryDatabase($"company-tests-{Guid.NewGuid()}")
            .EnableSensitiveDataLogging()
            .Options;

        return new CompanyDbContext(options);
    }

    public static async Task<CompanyEntity> SeedCompanyAsync(
        CompanyDbContext databaseContext,
        Guid userId,
        string name,
        string description = "",
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var company = new CompanyEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = name,
            Description = description,
            CreatedAt = now,
            UpdatedAt = now
        };
        databaseContext.Companies.Add(company);
        await databaseContext.SaveChangesAsync(cancellationToken);
        return company;
    }
}
