using FluentAssertions;
using NUnit.Framework;
using Sellevate.Organization.Infrastructure.Configuration;

namespace Sellevate.Organization.Tests.Unit;

/// <summary>
/// Pins <see cref="FrontendConfiguration.PrimaryUrl"/>, which exists because <c>Frontend:Url</c> is a
/// comma-separated list of allowed CORS origins rather than a single address — `docker-compose.yml`
/// ships <c>http://localhost:3000,https://sellevate.site</c> as its default.
///
/// <para>
/// Anything that builds a link for an email must take one origin out of that list. Using the raw value
/// yields <c>http://localhost:3000,https://sellevate.site/register</c>, which is broken in every
/// environment except a single-origin local one — that is, broken everywhere except where it would be
/// noticed. These tests fail if somebody swaps the property back.
/// </para>
/// </summary>
[TestFixture]
public sealed class FrontendConfigurationTests
{
    [Test]
    public void A_single_origin_is_returned_unchanged()
    {
        var configuration = new FrontendConfiguration { Url = "https://sellevate.example" };

        configuration.PrimaryUrl.Should().Be("https://sellevate.example");
    }

    [Test]
    public void The_first_origin_wins_when_the_value_is_a_cors_allow_list()
    {
        var configuration = new FrontendConfiguration
        {
            Url = "http://localhost:3000,https://sellevate.site"
        };

        configuration.PrimaryUrl.Should().Be("http://localhost:3000");
    }

    [Test]
    public void Whitespace_around_the_separators_is_not_carried_into_the_link()
    {
        var configuration = new FrontendConfiguration
        {
            Url = " http://localhost:3000 , https://sellevate.site "
        };

        configuration.PrimaryUrl.Should().Be("http://localhost:3000");
    }

    [Test]
    public void An_empty_value_falls_back_to_the_local_origin_rather_than_producing_a_bare_path()
    {
        var configuration = new FrontendConfiguration { Url = string.Empty };

        configuration.PrimaryUrl.Should().Be("http://localhost:3000");
    }
}
