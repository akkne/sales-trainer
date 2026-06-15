using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using SalesTrainer.Api.Features.Auth.Models;
using SalesTrainer.Api.Infrastructure.Data;
using SalesTrainer.Tests;
using SalesTrainer.Tests.Helpers;

namespace SalesTrainer.Tests.Integration;

[TestFixture]
public class AuthTests
{
    private HttpClient _client = null!;
    private AppDbContext _db = null!;
    private IServiceScope _scope = null!;

    [SetUp]
    public void SetUp()
    {
        _client = IntegrationTestSetup.Factory.CreateClient();
        _scope = IntegrationTestSetup.Factory.Services.CreateScope();
        _db = _scope.ServiceProvider.GetRequiredService<AppDbContext>();
    }

    [TearDown]
    public void TearDown()
    {
        _scope.Dispose();
    }

    [Test]
    public async Task Register_ValidCredentials_Returns200RequiringVerification()
    {
        var email = $"reg_{Guid.NewGuid()}@test.com";

        var response = await _client.PostAsJsonAsync("/auth/register", new
        {
            email,
            password = "Password123!",
            displayName = "New User"
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("requiresEmailVerification").GetBoolean().Should().BeTrue();
        body.GetProperty("email").GetString().Should().Be(email);

        var createdUser = _db.Users.First(user => user.Email == email);
        createdUser.IsEmailVerified.Should().BeFalse();
    }

    [Test]
    public async Task VerifyEmail_WithCodeFromEmail_Returns200WithAccessToken()
    {
        var email = $"verify_{Guid.NewGuid()}@test.com";
        await _client.PostAsJsonAsync("/auth/register", new
        {
            email,
            password = "Password123!",
            displayName = "Verify User"
        });

        var code = IntegrationTestSetup.Factory.EmailSender.GetLastCodeFor(email);
        code.Should().NotBeNullOrEmpty();

        var response = await _client.PostAsJsonAsync("/auth/verify-email", new { email, code });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("accessToken").GetString().Should().NotBeNullOrEmpty();
    }

    [Test]
    public async Task VerifyEmail_WrongCode_Returns401()
    {
        var email = $"badcode_{Guid.NewGuid()}@test.com";
        await _client.PostAsJsonAsync("/auth/register", new
        {
            email,
            password = "Password123!",
            displayName = "Bad Code User"
        });

        var response = await _client.PostAsJsonAsync("/auth/verify-email", new { email, code = "000001" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Login_UnverifiedEmail_Returns403RequiringVerification()
    {
        var email = $"unver_{Guid.NewGuid()}@test.com";
        await TestDbSeeder.SeedUserAsync(_db, email: email, isEmailVerified: false);

        var response = await _client.PostAsJsonAsync("/auth/login", new
        {
            email,
            password = "Password123!"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("requiresEmailVerification").GetBoolean().Should().BeTrue();
    }

    [Test]
    public async Task ResendCode_UnknownEmail_Returns204WithoutEnumeration()
    {
        var response = await _client.PostAsJsonAsync("/auth/resend-code", new
        {
            email = $"ghost_{Guid.NewGuid()}@test.com"
        });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Test]
    public async Task Register_DuplicateEmail_Returns409()
    {
        var email = $"dup_{Guid.NewGuid()}@test.com";
        await TestDbSeeder.SeedUserAsync(_db, email: email);

        var response = await _client.PostAsJsonAsync("/auth/register", new
        {
            email,
            password = "Password123!",
            displayName = "Dup User"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Test]
    public async Task Login_ValidCredentials_Returns200WithAccessToken()
    {
        var email = $"login_{Guid.NewGuid()}@test.com";
        await TestDbSeeder.SeedUserAsync(_db, email: email);

        var response = await _client.PostAsJsonAsync("/auth/login", new
        {
            email,
            password = "Password123!"
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("accessToken").GetString().Should().NotBeNullOrEmpty();
    }

    [Test]
    public async Task Login_WrongPassword_Returns401()
    {
        var email = $"wrong_{Guid.NewGuid()}@test.com";
        await TestDbSeeder.SeedUserAsync(_db, email: email);

        var response = await _client.PostAsJsonAsync("/auth/login", new
        {
            email,
            password = "WrongPassword!"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task GetMe_WithValidToken_Returns200WithUserInfo()
    {
        var user = await TestDbSeeder.SeedUserAsync(_db, email: $"me_{Guid.NewGuid()}@test.com");
        var authedClient = IntegrationTestSetup.Factory.CreateAuthenticatedClient(
            user.Id, user.Email, user.DisplayName);

        var response = await authedClient.GetAsync("/auth/me");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("email").GetString().Should().Be(user.Email);
        body.GetProperty("role").GetString().Should().Be("User");
    }

    [Test]
    public async Task GetMe_WithoutToken_Returns401()
    {
        var response = await _client.GetAsync("/auth/me");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Refresh_ValidCookie_Returns200WithNewAccessToken()
    {
        var user = await TestDbSeeder.SeedUserAsync(_db,
            email: $"rf_{Guid.NewGuid()}@test.com");

        var rawToken = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(64));
        _db.Set<RefreshToken>().Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Token = rawToken,
            ExpiresAt = DateTime.UtcNow.AddDays(30),
            IsRevoked = false
        });
        await _db.SaveChangesAsync();

        var request = new HttpRequestMessage(HttpMethod.Post, "/auth/refresh");
        request.Headers.Add("Cookie", $"refreshToken={Uri.EscapeDataString(rawToken)}");

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("accessToken").GetString().Should().NotBeNullOrEmpty();
    }

    [Test]
    public async Task Refresh_NoCookie_Returns401()
    {
        var response = await _client.PostAsync("/auth/refresh", null);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Logout_RevokesToken_DeletesCookie()
    {
        var user = await TestDbSeeder.SeedUserAsync(_db,
            email: $"lo_{Guid.NewGuid()}@test.com");

        var rawToken = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(64));
        _db.Set<RefreshToken>().Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Token = rawToken,
            ExpiresAt = DateTime.UtcNow.AddDays(30),
            IsRevoked = false
        });
        await _db.SaveChangesAsync();

        var logoutRequest = new HttpRequestMessage(HttpMethod.Post, "/auth/logout");
        logoutRequest.Headers.Add("Cookie", $"refreshToken={Uri.EscapeDataString(rawToken)}");
        var logoutResponse = await _client.SendAsync(logoutRequest);
        logoutResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var refreshRequest = new HttpRequestMessage(HttpMethod.Post, "/auth/refresh");
        refreshRequest.Headers.Add("Cookie", $"refreshToken={Uri.EscapeDataString(rawToken)}");
        var refreshAfterLogout = await _client.SendAsync(refreshRequest);
        refreshAfterLogout.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
