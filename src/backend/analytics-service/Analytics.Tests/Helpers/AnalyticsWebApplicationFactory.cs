using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;
using Sellevate.Analytics.Common.Constants;
using Sellevate.Analytics.Features.Presence.Services.Abstract;
using StackExchange.Redis;

namespace Sellevate.Analytics.Tests.Helpers;

/// <summary>
/// Test host for analytics-service integration tests. Both the Redis multiplexer and the presence
/// tracker that talks to it are replaced with substitutes, so the suite needs no Docker and no
/// broker: what is under test here is the HTTP pipeline — authentication, tenancy and model
/// binding — not Redis.
/// </summary>
public sealed class AnalyticsWebApplicationFactory : WebApplicationFactory<Program>
{
    /// <summary>Exposes the stubbed presence tracker so tests can configure return values.</summary>
    public IPresenceTracker PresenceTracker { get; } = Substitute.For<IPresenceTracker>();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"ConnectionStrings:{ConfigurationKeys.RedisConnectionName}"] = "localhost:6379,abortConnect=false",
                ["Kafka:BootstrapServers"] = "localhost:9092",
                ["Kafka:ConsumerGroupId"] = "analytics-tests",
                [ConfigurationKeys.JwtKey] = JwtTestHelper.JwtKey,
                [ConfigurationKeys.JwtIssuer] = JwtTestHelper.JwtIssuer,
                [ConfigurationKeys.JwtAudience] = JwtTestHelper.JwtAudience,
                [ConfigurationKeys.FrontendUrl] = "http://localhost:3000",
                [ConfigurationKeys.LokiUrl] = "http://localhost:1/loki"
            });
        });

        builder.ConfigureServices(services =>
        {
            var stubRedis = Substitute.For<IConnectionMultiplexer>();
            var stubDb = Substitute.For<IDatabase>();
            stubRedis.GetDatabase(Arg.Any<int>(), Arg.Any<object?>()).Returns(stubDb);

            services.RemoveAll<IConnectionMultiplexer>();
            services.AddSingleton(stubRedis);

            services.RemoveAll<IPresenceTracker>();
            services.AddSingleton(PresenceTracker);
        });
    }

    public HttpClient CreateAuthenticatedClient(Guid? userId = null)
    {
        var client = CreateClient();
        var token = JwtTestHelper.BuildToken(userId ?? Guid.NewGuid());
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}
