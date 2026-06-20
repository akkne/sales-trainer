using DotNet.Testcontainers.Builders;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Sellevate.Identity.Infrastructure.Data;
using Sellevate.Identity.Tests.Helpers;
using Testcontainers.PostgreSql;

// Scoped to the Integration namespace so the Postgres testcontainer starts only for
// integration tests; Unit tests must run without a docker daemon.
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

        // Touch the host so Program.cs runs Database.Migrate() + seeding on startup.
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        await db.Database.MigrateAsync();
    }

    [OneTimeTearDown]
    public async Task StopAsync()
    {
        Factory.Dispose();
        await Postgres.DisposeAsync();
    }
}
