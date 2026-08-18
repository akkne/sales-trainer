using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Sellevate.Identity.Features.Auth.Constants;
using Sellevate.Identity.Features.Auth.Models;
using Sellevate.Identity.Infrastructure.Data;
using Sellevate.Identity.Tests.Helpers;

namespace Sellevate.Identity.Tests.Integration;

/// <summary>
/// Phase 40.8 — the three-step login flow (docs/TENANCY/TENANCY.md §4.5) end to end against the
/// real Postgres container, including the <c>text[]</c> domain lookup, which the in-memory
/// provider cannot exercise faithfully.
/// </summary>
[TestFixture]
[Category("Integration")]
public class LoginMethodFlowTests
{
    private TestWebApplicationFactory Factory => IntegrationTestSetup.Factory;

    private static string UniqueDomain() => $"customer-{Guid.NewGuid():N}.test";

    [Test]
    public async Task StartLogin_ForAnUnknownAddress_ReturnsPasswordWithoutRevealingAnything()
    {
        var client = Factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/auth/login/start", new { email = $"stranger-{Guid.NewGuid():N}@nowhere.test" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<LoginStartResponseDto>();
        body!.Method.Should().Be(AuthMethodNames.Password);
    }

    /// <summary>
    /// The enumeration guard. An outsider walking a list of addresses must not be able to tell a
    /// Sellevate customer's employee from a stranger — the same property 40.7 gave
    /// <c>POST /auth/google</c> by answering one identical <c>401</c> for both cases.
    /// </summary>
    [Test]
    public async Task StartLogin_ForKnownAndUnknownAddresses_ReturnsIdenticalResponses()
    {
        var organizationId = Guid.NewGuid();
        var knownEmail = $"member-{Guid.NewGuid():N}@known.test";
        await TestUserSeeder.SeedUserAsync(Factory, knownEmail, "Member", organizationId: organizationId);
        var client = Factory.CreateClient();

        var knownResponse = await client.PostAsJsonAsync("/auth/login/start", new { email = knownEmail });
        var unknownResponse = await client.PostAsJsonAsync(
            "/auth/login/start", new { email = $"stranger-{Guid.NewGuid():N}@known.test" });

        knownResponse.StatusCode.Should().Be(unknownResponse.StatusCode);
        var knownBody = await knownResponse.Content.ReadAsStringAsync();
        var unknownBody = await unknownResponse.Content.ReadAsStringAsync();
        knownBody.Should().Be(unknownBody, "the first login step must not be an enumeration oracle");
    }

    [Test]
    public async Task StartLogin_WithoutAnOrganizationHeader_IsAllowed()
    {
        var client = Factory.CreateClient();

        var response = await client.PostAsJsonAsync("/auth/login/start", new { email = "someone@nowhere.test" });

        response.StatusCode.Should().Be(
            HttpStatusCode.OK,
            "the step exists precisely because the caller has no organization yet");
    }

    [Test]
    public async Task StartLogin_WithAMalformedAddress_IsBadRequest()
    {
        var client = Factory.CreateClient();

        var response = await client.PostAsJsonAsync("/auth/login/start", new { email = "not-an-email" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task StartLogin_ForAConfiguredSsoDomain_ReturnsThatMethod()
    {
        var organizationId = Guid.NewGuid();
        var domain = UniqueDomain();
        await SeedAuthConfigurationAsync(organizationId, AuthMethodNames.Oidc, [domain]);
        var client = Factory.CreateClient();

        var response = await client.PostAsJsonAsync("/auth/login/start", new { email = $"newcomer@{domain}" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<LoginStartResponseDto>();
        body!.Method.Should().Be(AuthMethodNames.Oidc);
    }

    /// <summary>
    /// Step 3: dispatch. There is no OIDC provider, so a password is refused for that organization
    /// even when it is the right password — the seam disables password login the moment a customer
    /// is switched to their own directory.
    /// </summary>
    [Test]
    public async Task Login_WithCorrectPasswordButSsoConfigured_IsUnauthorized()
    {
        var organizationId = Guid.NewGuid();
        var domain = UniqueDomain();
        var email = $"employee@{domain}";
        await TestUserSeeder.SeedUserAsync(Factory, email, "Employee", organizationId: organizationId);
        await SeedAuthConfigurationAsync(organizationId, AuthMethodNames.Oidc, [domain]);
        var client = Factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/auth/login", new { email, password = TestUserSeeder.DefaultPassword });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Login_WithCorrectPasswordAndPasswordConfigured_StillSucceeds()
    {
        var organizationId = Guid.NewGuid();
        var domain = UniqueDomain();
        var email = $"employee@{domain}";
        await TestUserSeeder.SeedUserAsync(Factory, email, "Employee", organizationId: organizationId);
        await SeedAuthConfigurationAsync(organizationId, AuthMethodNames.Password, [domain]);
        var client = Factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/auth/login", new { email, password = TestUserSeeder.DefaultPassword });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>
    /// The table is intentionally outside row-level security, so a plain scope with no tenant
    /// context can read and write it — the property the pre-authentication login step depends on.
    /// </summary>
    [Test]
    public async Task OrganizationAuthConfiguration_IsReadableWithoutATenantContext()
    {
        var organizationId = Guid.NewGuid();
        await SeedAuthConfigurationAsync(organizationId, AuthMethodNames.Password, [UniqueDomain()]);

        using var scope = Factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();

        var stored = await database.OrganizationAuthConfigurations
            .SingleOrDefaultAsync(configuration => configuration.OrganizationId == organizationId);

        stored.Should().NotBeNull();
    }

    private async Task SeedAuthConfigurationAsync(Guid organizationId, string method, string[] allowedEmailDomains)
    {
        using var scope = Factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        database.OrganizationAuthConfigurations.Add(new OrganizationAuthConfiguration
        {
            OrganizationId = organizationId,
            Method = method,
            AllowedEmailDomains = allowedEmailDomains,
            CreatedAt = DateTime.UtcNow
        });
        await database.SaveChangesAsync();
    }
}
