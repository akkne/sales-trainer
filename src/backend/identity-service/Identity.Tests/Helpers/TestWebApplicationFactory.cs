using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Sellevate.BuildingBlocks.Email.Abstract;
using Sellevate.BuildingBlocks.Identity;
using Sellevate.Identity.Eventing;
using Sellevate.Identity.Features.Auth.Models;

namespace Sellevate.Identity.Tests.Helpers;

/// <summary>
/// Hosts the real identity-service in-process for the integration suite, with the email sender and
/// the user-event publisher swapped for recording doubles.
/// </summary>
public sealed class TestWebApplicationFactory(string connectionString) : WebApplicationFactory<Program>
{
    public RecordingEmailSender EmailSender { get; } = new();
    public RecordingUserEventPublisher UserEventPublisher { get; } = new();

    /// <summary>
    /// Program.cs uses the minimal hosting model, so its top-level statements read
    /// <c>builder.Configuration</c> while the host is still being constructed — before
    /// <see cref="WebApplicationFactory{TEntryPoint}"/> gets to apply
    /// <c>ConfigureAppConfiguration</c>. Anything Program.cs validates eagerly (<c>Jwt:Key</c>) must
    /// therefore already be in the environment by the time the host is built, which is why
    /// <see cref="ExportSettingsToEnvironment"/> publishes the same dictionary as environment
    /// variables: the default configuration sources pick those up first.
    /// </summary>
    /// <summary>The shared secret <see cref="CreateInternalServiceClient"/> attaches, standing in for
    /// organization-service's <c>InternalAuth:ServiceSecret</c> in production.</summary>
    public const string InternalServiceSecret = "identity-internal-service-test-secret";

    private const string InternalServiceSecretHeaderName = "X-Internal-Service-Secret";

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
        ["Logging:Loki:Url"] = "http://localhost:1/loki",
        ["InternalAuth:ServiceSecret"] = InternalServiceSecret
    };

    public static void ExportSettingsToEnvironment(string connectionString)
    {
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Testing");
        foreach (var (key, value) in BuildSettings(connectionString))
        {
            Environment.SetEnvironmentVariable(key.Replace(":", "__"), value);
        }
    }

    /// <summary>
    /// Phase 40.9: <see cref="OrganizationReplicaConsumer"/> resolves the Redis-backed idempotency
    /// store the moment the host starts, and there is no Redis in the test environment, so it is
    /// removed here. Removed by exact implementation type rather than with
    /// <c>RemoveAll&lt;IHostedService&gt;()</c> so the outbox relay and the topic provisioner keep
    /// running exactly as before. The tests seed <c>OrganizationReplicas</c> directly through
    /// <see cref="TestOrganizationSeeder"/> — exactly what the consumer would have written — and the
    /// projection it performs is covered separately by <c>OrganizationReplicaProjectorTests</c>.
    /// </summary>
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

    /// <summary>
    /// Stands in for the gateway, which is the only thing allowed to set <c>X-Organization-Id</c> and
    /// always derives it from the validated token (docs/TENANCY/TENANCY.md §1.1). Calling the service
    /// directly in a test means setting it by hand.
    ///
    /// <para>
    /// <paramref name="organizationRole"/> defaults to <c>TenancySuperAdmin</c> rather than
    /// <c>TenancyAdmin</c> because most callers use this client to invite or offboard someone, and
    /// after the 2026-08-16 role split that is the one thing only a superadmin may do
    /// (docs/DECISIONS.md). Pass it explicitly to assert the negative case.
    /// </para>
    /// </summary>
    /// <summary>
    /// Stands in for organization-service calling an <c>internal/*</c> route: no JWT, just the shared
    /// secret header. Pass an explicit <paramref name="serviceSecret"/> to assert the wrong/missing-
    /// secret case.
    /// </summary>
    public HttpClient CreateInternalServiceClient(string? serviceSecret = InternalServiceSecret)
    {
        var client = CreateClient();
        if (serviceSecret is not null)
        {
            client.DefaultRequestHeaders.Add(InternalServiceSecretHeaderName, serviceSecret);
        }

        return client;
    }

    public HttpClient CreateOrganizationAdminClient(
        Guid userId,
        Guid organizationId,
        string email = "orgadmin@test.com",
        string organizationRole = "TenancySuperAdmin")
    {
        var client = CreateAuthenticatedClient(
            userId, email, "Org Admin", UserRole.User, organizationId, organizationRole);
        client.DefaultRequestHeaders.Add(IdentityHeaders.OrganizationId, organizationId.ToString());
        return client;
    }
}
