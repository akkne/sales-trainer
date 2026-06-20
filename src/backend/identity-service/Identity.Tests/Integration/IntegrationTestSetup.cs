using DotNet.Testcontainers.Builders;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Sellevate.Identity.Infrastructure.Data;
using Sellevate.Identity.Tests.Helpers;
using Testcontainers.PostgreSql;

namespace Sellevate.Identity.Tests.Integration;

[SetUpFixture]
public class IntegrationTestSetup
{
    public static PostgreSqlContainer Postgres { get; private set; } = null!;
    public static TestWebApplicationFactory Factory { get; private set; } = null!;

    [OneTimeSetUp]
    public async Task StartAsync()
    {
        Postgres = new PostgreSqlBuilder()
            .WithImage("postgres:17-alpine")
            .WithDatabase("identity_test")
            .WithUsername("identity_test")
            .WithPassword("identity_pass")
            .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(5432))
            .Build();

        await Postgres.StartAsync();

        Factory = new TestWebApplicationFactory(Postgres.GetConnectionString());

        using var scope = Factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        await database.Database.MigrateAsync();
    }

    [OneTimeTearDown]
    public async Task StopAsync()
    {
        Factory.Dispose();
        await Postgres.DisposeAsync();
    }
}
