using FluentAssertions;
using Microsoft.Extensions.Configuration;
using NUnit.Framework;
using Sellevate.BuildingBlocks.Persistence;

namespace Sellevate.BuildingBlocks.Tests.Persistence;

[TestFixture]
public class PostgresConnectionStringsTests
{
    private const string RuntimeConnectionString = "Host=postgres;Database=learning;Username=sellevate_app;Password=app";

    private const string MigrationConnectionString = "Host=postgres;Database=learning;Username=owner;Password=owner";

    [Test]
    public void Migrations_falls_back_to_the_runtime_connection_when_no_separate_one_is_configured()
    {
        var configuration = Build(("ConnectionStrings:Postgres", RuntimeConnectionString));

        PostgresConnectionStrings.Migrations(configuration).Should().Be(RuntimeConnectionString);
        PostgresConnectionStrings.IsSplitConfigured(configuration).Should().BeFalse();
    }

    [Test]
    public void Migrations_prefers_the_dedicated_connection_when_it_is_configured()
    {
        var configuration = Build(
            ("ConnectionStrings:Postgres", RuntimeConnectionString),
            ("ConnectionStrings:PostgresMigrations", MigrationConnectionString));

        PostgresConnectionStrings.Migrations(configuration).Should().Be(MigrationConnectionString);
        PostgresConnectionStrings.Runtime(configuration).Should().Be(RuntimeConnectionString);
        PostgresConnectionStrings.IsSplitConfigured(configuration).Should().BeTrue();
    }

    /// <summary>
    /// An unset compose variable arrives as an empty string, not as an absent key. Treating that as
    /// "configured" would hand every migration an empty connection string and fail the boot of a
    /// deployment whose only mistake was leaving the new variable blank.
    /// </summary>
    [Test]
    public void Migrations_treats_a_blank_dedicated_connection_as_absent()
    {
        var configuration = Build(
            ("ConnectionStrings:Postgres", RuntimeConnectionString),
            ("ConnectionStrings:PostgresMigrations", "   "));

        PostgresConnectionStrings.Migrations(configuration).Should().Be(RuntimeConnectionString);
        PostgresConnectionStrings.IsSplitConfigured(configuration).Should().BeFalse();
    }

    [Test]
    public void Runtime_throws_when_the_connection_string_is_missing()
    {
        var configuration = Build();

        var act = () => PostgresConnectionStrings.Runtime(configuration);

        act.Should().Throw<InvalidOperationException>().WithMessage("*ConnectionStrings:Postgres*");
    }

    [Test]
    public void Describe_names_the_role_without_leaking_the_password()
    {
        var description = PostgresConnectionStrings.Describe(MigrationConnectionString);

        description.Should().Be("postgres:5432/learning as owner");
        description.Should().NotContain("owner;Password");
    }

    private static IConfiguration Build(params (string Key, string Value)[] values)
        => new ConfigurationBuilder()
            .AddInMemoryCollection(values.Select(value => new KeyValuePair<string, string?>(value.Key, value.Value)))
            .Build();
}
