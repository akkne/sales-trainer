using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Sellevate.Identity.Eventing;
using Sellevate.Identity.Features.Auth.Models;
using Sellevate.BuildingBlocks.Email.Abstract;
using Sellevate.BuildingBlocks.Identity;

namespace Sellevate.Identity.Tests.Helpers;

public sealed class TestWebApplicationFactory(string connectionString) : WebApplicationFactory<Program>
{
    public RecordingEmailSender EmailSender { get; } = new();
    public RecordingUserEventPublisher UserEventPublisher { get; } = new();

    // Program.cs uses the minimal hosting model, so its top-level statements read
    // builder.Configuration directly while the host is being constructed — which happens
    // before WebApplicationFactory gets to apply ConfigureAppConfiguration below. Anything
    // Program.cs validates eagerly (Jwt:Key) must therefore already be in the environment
    // by the time the host is built, so the settings are also exported as environment
    // variables, which the default configuration sources pick up first.
    private static Dictionary<string, string?> BuildSettings(string connectionString) => new()
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
    };

    public static void ExportSettingsToEnvironment(string connectionString)
    {
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Testing");
        foreach (var (key, value) in BuildSettings(connectionString))
        {
            Environment.SetEnvironmentVariable(key.Replace(":", "__"), value);
        }
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(BuildSettings(connectionString));
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IEmailSender>();
            services.AddSingleton<IEmailSender>(EmailSender);

            services.RemoveAll<IUserEventPublisher>();
            services.AddSingleton<IUserEventPublisher>(UserEventPublisher);

            // Phase 40.9: OrganizationReplicaConsumer resolves the Redis-backed idempotency
            // store the moment the host starts, and there is no Redis in the test environment.
            // Removed by exact implementation type rather than with RemoveAll<IHostedService>()
            // so the outbox relay and topic provisioner keep running exactly as before. The tests
            // seed OrganizationReplicas directly (TestOrganizationSeeder) — exactly what the
            // consumer would have written — and the projection it performs is covered separately
            // by OrganizationReplicaProjectorTests.
            var organizationReplicaConsumerDescriptors = services
                .Where(descriptor => descriptor.ImplementationType == typeof(OrganizationReplicaConsumer))
                .ToList();
            foreach (var descriptor in organizationReplicaConsumerDescriptors)
            {
                services.Remove(descriptor);
            }
        });
    }

    public HttpClient CreateAuthenticatedClient(Guid userId, string email, string displayName,
        UserRole role = UserRole.User, Guid? organizationId = null, string? orgRole = null)
    {
        var client = CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        var token = JwtTestHelper.BuildToken(userId, email, displayName, role, organizationId, orgRole);
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    // Stands in for the gateway, which is the only thing allowed to set X-Organization-Id and
    // always derives it from the validated token (docs/TENANCY/TENANCY.md §1.1). Calling the
    // service directly in a test means setting it by hand.
    public HttpClient CreateOrganizationAdminClient(Guid userId, Guid organizationId, string email = "orgadmin@test.com")
    {
        var client = CreateAuthenticatedClient(
            userId, email, "Org Admin", UserRole.User, organizationId, "OrgAdmin");
        client.DefaultRequestHeaders.Add(IdentityHeaders.OrganizationId, organizationId.ToString());
        return client;
    }
}
