using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;
using Sellevate.Analytics.Features.Presence.Services.Abstract;
using StackExchange.Redis;

namespace Sellevate.Analytics.Tests.Helpers;

/// <summary>
/// Test host for analytics-service integration tests.
/// Redis and Kafka are stubbed so tests run without Docker.
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
                ["ConnectionStrings:Redis"] = "localhost:6379,abortConnect=false",
                ["Kafka:BootstrapServers"] = "localhost:9092",
                ["Kafka:ConsumerGroupId"] = "analytics-tests",
                ["Jwt:Key"] = JwtTestHelper.JwtKey,
                ["Jwt:Issuer"] = JwtTestHelper.JwtIssuer,
                ["Jwt:Audience"] = JwtTestHelper.JwtAudience,
                ["Frontend:Url"] = "http://localhost:3000",
                ["Logging:Loki:Url"] = "http://localhost:1/loki"
            });
        });

        builder.ConfigureServices(services =>
        {
            // Replace the real Redis connection with a stub so tests never need Docker.
            var stubRedis = Substitute.For<IConnectionMultiplexer>();
            var stubDb = Substitute.For<IDatabase>();
            stubRedis.GetDatabase(Arg.Any<int>(), Arg.Any<object?>()).Returns(stubDb);

            services.RemoveAll<IConnectionMultiplexer>();
            services.AddSingleton(stubRedis);

            // Replace the real presence tracker (which talks to Redis) with our stub.
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
