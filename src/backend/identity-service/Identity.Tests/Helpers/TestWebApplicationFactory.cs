using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Sellevate.Identity.Eventing;
using Sellevate.Identity.Features.Auth.Models;
using Sellevate.Identity.Infrastructure.Email.Abstract;

namespace Sellevate.Identity.Tests.Helpers;

public sealed class TestWebApplicationFactory(string connectionString) : WebApplicationFactory<Program>
{
    public RecordingEmailSender EmailSender { get; } = new();
    public RecordingUserEventPublisher UserEventPublisher { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Postgres"] = connectionString,
                ["Kafka:BootstrapServers"] = "localhost:9092",
                ["Jwt:Key"] = JwtTestHelper.JwtKey,
                ["Jwt:Issuer"] = JwtTestHelper.JwtIssuer,
                ["Jwt:Audience"] = JwtTestHelper.JwtAudience,
                ["Google:ClientId"] = "test-google-client-id",
                ["MailerSend:ApiToken"] = "",
                ["MailerSend:FromEmail"] = "noreply@test.com",
                ["SuperAdmin:Email"] = "superadmin@test.com",
                ["SuperAdmin:Password"] = "SuperAdmin123!",
                ["SuperAdmin:DisplayName"] = "Test SuperAdmin",
                ["Storage:S3:Endpoint"] = "http://localhost:9000",
                ["Storage:S3:Bucket"] = "identity-tests",
                ["Storage:S3:AccessKey"] = "minioadmin",
                ["Storage:S3:SecretKey"] = "minioadmin",
                ["Logging:Loki:Url"] = "http://localhost:1/loki"
            });
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IEmailSender>();
            services.AddSingleton<IEmailSender>(EmailSender);

            services.RemoveAll<IUserEventPublisher>();
            services.AddSingleton<IUserEventPublisher>(UserEventPublisher);
        });
    }

    public HttpClient CreateAuthenticatedClient(Guid userId, string email, string displayName,
        UserRole role = UserRole.User)
    {
        var client = CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        var token = JwtTestHelper.BuildToken(userId, email, displayName, role);
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}
