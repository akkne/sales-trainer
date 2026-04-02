using DotNet.Testcontainers.Builders;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using SalesTrainer.Api.Infrastructure.Data;
using SalesTrainer.Tests.Helpers;
using Testcontainers.PostgreSql;

namespace SalesTrainer.Tests;

[SetUpFixture]
public class IntegrationTestSetup
{
    public static PostgreSqlContainer Postgres { get; private set; } = null!;
    public static TestWebApplicationFactory Factory { get; private set; } = null!;

    [OneTimeSetUp]
    public async Task StartAsync()
    {
        Postgres = new PostgreSqlBuilder()
            .WithImage("postgres:16-alpine")
            .WithDatabase("st_test")
            .WithUsername("st_test")
            .WithPassword("st_pass")
            .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(5432))
            .Build();

        await Postgres.StartAsync();

        Factory = new TestWebApplicationFactory(Postgres.GetConnectionString());

        // Trigger app startup which runs Database.Migrate() automatically
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();
    }

    [OneTimeTearDown]
    public async Task StopAsync()
    {
        Factory.Dispose();
        await Postgres.DisposeAsync();
    }
}
