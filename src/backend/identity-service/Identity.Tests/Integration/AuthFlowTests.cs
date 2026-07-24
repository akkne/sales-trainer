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

    // TEMP: email confirmation disabled — registration returns tokens immediately.
    [Test]
    public async Task Register_ReturnsTokens_AndEmitsUserRegistered()
    {
        var client = Factory.CreateClient();
        var email = UniqueEmail();

        var response = await client.PostAsJsonAsync("/auth/register",
            new { email, password = "Password123!", displayName = "Reg User" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<AuthTokenResult>();
        body!.AccessToken.Should().NotBeNullOrEmpty();

        Factory.UserEventPublisher.Registered.Should().Contain(e => e.Email == email);
    }

    [Test]
    public async Task Register_Duplicate_ReturnsConflict()
    {
        var client = Factory.CreateClient();
        var email = UniqueEmail();
        var payload = new { email, password = "Password123!", displayName = "Dup" };

        (await client.PostAsJsonAsync("/auth/register", payload)).EnsureSuccessStatusCode();
        var second = await client.PostAsJsonAsync("/auth/register", payload);

        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    // TEMP: email confirmation disabled — login succeeds immediately after registration.
    [Test]
    public async Task Login_AfterRegister_Succeeds_WithoutVerification()
    {
        var client = Factory.CreateClient();
        var email = UniqueEmail();
        const string password = "Password123!";

        await client.PostAsJsonAsync("/auth/register", new { email, password, displayName = "Flow" });

        var login = await client.PostAsJsonAsync("/auth/login", new { email, password });
        login.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await login.Content.ReadFromJsonAsync<AuthTokenResult>();
        body!.AccessToken.Should().NotBeNullOrEmpty();
    }

    [Test]
    public async Task Refresh_RotatesToken_ViaCookie()
    {
        var client = Factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            HandleCookies = true
        });
        var email = UniqueEmail();
        const string password = "Password123!";

        // TEMP: registration sets the refresh cookie directly (no verify-email step).
        await client.PostAsJsonAsync("/auth/register", new { email, password, displayName = "Refresh" });

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
        const string password = "Password123!";

        // TEMP: registration returns the refresh cookie directly (no verify-email step).
        var register = await client.PostAsJsonAsync("/auth/register", new { email, password, displayName = "Reuse" });
        var firstRefreshToken = ExtractRefreshTokenCookie(register);

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

    private sealed record AuthTokenResult(string AccessToken, string UserId, string DisplayName, bool IsOnboardingCompleted, string Role);
}
