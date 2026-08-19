using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using NUnit.Framework;
using Sellevate.Identity.Tests.Helpers;

namespace Sellevate.Identity.Tests.Integration;

[TestFixture]
[Category("Integration")]
public class AuthFlowTests
{
    private TestWebApplicationFactory Factory => IntegrationTestSetup.Factory;

    private static string UniqueEmail() => $"user-{Guid.NewGuid():N}@test.com";

    [Test]
    public async Task Health_ReturnsOk()
    {
        var client = Factory.CreateClient();
        var response = await client.GetAsync("/healthz");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>
    /// Phase 40.37 reopened the route 40.7 deleted (docs/TENANCY/TENANCY.md §4.1a). What is asserted
    /// is not just that it answers, but the shape of what it hands back: a session with no
    /// organization on it.
    /// </summary>
    [Test]
    public async Task Register_CreatesASessionWithNoOrganization()
    {
        var client = Factory.CreateClient();

        var response = await client.PostAsJsonAsync("/auth/register",
            new { email = UniqueEmail(), password = "Password123!", displayName = "Reg User" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<AuthTokenResult>();
        body!.AccessToken.Should().NotBeNullOrEmpty();
        body.OrgId.Should().BeNull("registering joins no company — only an invite does that");
        body.IsOnboardingCompleted.Should().BeFalse();
    }

    [Test]
    public async Task Register_SameAddressTwice_IsRejectedAsConflict()
    {
        var client = Factory.CreateClient();
        var email = UniqueEmail();
        var payload = new { email, password = "Password123!", displayName = "Reg User" };

        (await client.PostAsJsonAsync("/auth/register", payload)).StatusCode.Should().Be(HttpStatusCode.OK);
        var second = await client.PostAsJsonAsync("/auth/register", payload);

        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    /// <summary>
    /// The registered account must actually be able to come back — the invite flow used to be the
    /// only producer of a login-capable row, so this covers the password hash landing correctly.
    /// </summary>
    [Test]
    public async Task Register_ThenLogin_Succeeds()
    {
        var client = Factory.CreateClient();
        var email = UniqueEmail();
        await client.PostAsJsonAsync("/auth/register",
            new { email, password = "Password123!", displayName = "Reg User" });

        var login = await client.PostAsJsonAsync("/auth/login", new { email, password = "Password123!" });

        login.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Test]
    public async Task Login_AfterSeeding_Succeeds()
    {
        var client = Factory.CreateClient();
        var email = UniqueEmail();
        await TestUserSeeder.SeedUserAsync(Factory, email, "Flow");

        var login = await client.PostAsJsonAsync("/auth/login",
            new { email, password = TestUserSeeder.DefaultPassword });

        login.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await login.Content.ReadFromJsonAsync<AuthTokenResult>();
        body!.AccessToken.Should().NotBeNullOrEmpty();
    }

    /// <summary>
    /// Outside Development the refresh cookie is issued with <c>Secure=true</c>, so the client has to
    /// talk https or the cookie container never sends it back and <c>/auth/refresh</c> sees none.
    /// </summary>
    [Test]
    public async Task Refresh_RotatesToken_ViaCookie()
    {
        var client = Factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            HandleCookies = true,
            BaseAddress = new Uri("https://localhost")
        });
        var email = UniqueEmail();
        await TestUserSeeder.SeedUserAsync(Factory, email, "Refresh");

        var login = await client.PostAsJsonAsync("/auth/login",
            new { email, password = TestUserSeeder.DefaultPassword });
        login.StatusCode.Should().Be(HttpStatusCode.OK);

        var refresh = await client.PostAsync("/auth/refresh", null);
        refresh.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Test]
    public async Task Refresh_WithReusedRevokedToken_RevokesEntireFamily()
    {
        var client = Factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            HandleCookies = false
        });
        var email = UniqueEmail();
        await TestUserSeeder.SeedUserAsync(Factory, email, "Reuse");

        var login = await client.PostAsJsonAsync("/auth/login",
            new { email, password = TestUserSeeder.DefaultPassword });
        var firstRefreshToken = ExtractRefreshTokenCookie(login);

        var firstRefresh = await PostRefreshWithCookie(client, firstRefreshToken);
        firstRefresh.StatusCode.Should().Be(HttpStatusCode.OK);
        var rotatedRefreshToken = ExtractRefreshTokenCookie(firstRefresh);

        var reusedOldToken = await PostRefreshWithCookie(client, firstRefreshToken);
        reusedOldToken.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var rotatedTokenAfterReuse = await PostRefreshWithCookie(client, rotatedRefreshToken);
        rotatedTokenAfterReuse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private static string ExtractRefreshTokenCookie(HttpResponseMessage response)
    {
        var setCookie = response.Headers.GetValues("Set-Cookie").First(value => value.StartsWith("refreshToken="));
        return setCookie["refreshToken=".Length..setCookie.IndexOf(';')];
    }

    private static Task<HttpResponseMessage> PostRefreshWithCookie(HttpClient client, string refreshToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/auth/refresh");
        request.Headers.Add("Cookie", $"refreshToken={refreshToken}");
        return client.SendAsync(request);
    }

    private sealed record AuthTokenResult(
        string AccessToken,
        string UserId,
        string DisplayName,
        bool IsOnboardingCompleted,
        string Role,
        string? OrgId,
        string? OrgRole);
}
