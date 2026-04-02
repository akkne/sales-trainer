using Hangfire;
using Hangfire.InMemory;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace SalesTrainer.Tests.Helpers;

public class TestWebApplicationFactory(string connectionString) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Postgres"] = connectionString,
                ["ConnectionStrings:Redis"] = "localhost:0,abortConnect=false",
                ["ConnectionStrings:Mongo"] = "mongodb://localhost:1/?serverSelectionTimeoutMS=100",
                ["Jwt:Key"] = JwtTestHelper.JwtKey,
                ["Jwt:Issuer"] = JwtTestHelper.JwtIssuer,
                ["Jwt:Audience"] = JwtTestHelper.JwtAudience,
                ["OpenAI:ApiKey"] = "",
                ["Google:ClientId"] = "test-google-client-id",
                ["SuperAdmin:Email"] = "superadmin@test.com",
                ["SuperAdmin:Password"] = "SuperAdmin123!",
                ["SuperAdmin:DisplayName"] = "Test SuperAdmin",
                ["Logging:Loki:Url"] = "http://localhost:1/loki"
            });
        });

        builder.ConfigureServices(services =>
        {
            // Replace Hangfire PostgreSql with InMemory to avoid schema creation on test DB
            var hangfireDescriptors = services
                .Where(d => d.ServiceType.FullName?.Contains("Hangfire") == true)
                .ToList();
            foreach (var d in hangfireDescriptors)
                services.Remove(d);

            services.AddHangfire(c => c.UseInMemoryStorage());
            services.AddHangfireServer();
        });
    }

    public HttpClient CreateAuthenticatedClient(Guid userId, string email, string displayName,
        SalesTrainer.Api.Features.Auth.UserRole role = SalesTrainer.Api.Features.Auth.UserRole.User)
    {
        var client = CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        var token = JwtTestHelper.BuildToken(userId, email, displayName, role);
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}
