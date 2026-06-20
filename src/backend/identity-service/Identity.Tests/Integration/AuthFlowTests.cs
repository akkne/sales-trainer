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

    [Test]
    public async Task Register_RequiresVerification_SendsEmail_AndEmitsUserRegistered()
    {
        var client = Factory.CreateClient();
        var email = UniqueEmail();

        var response = await client.PostAsJsonAsync("/auth/register",
            new { email, password = "Password123!", displayName = "Reg User" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<RegistrationResult>();
        body!.RequiresEmailVerification.Should().BeTrue();

        Factory.EmailSender.SentMessages.Should().Contain(m => m.RecipientEmail == email);
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

    [Test]
    public async Task Login_BeforeVerification_IsForbidden_ThenSucceeds_AfterVerify()
    {
        var client = Factory.CreateClient();
        var email = UniqueEmail();
        const string password = "Password123!";

        await client.PostAsJsonAsync("/auth/register", new { email, password, displayName = "Flow" });

        // Not verified yet → 403.
        var blocked = await client.PostAsJsonAsync("/auth/login", new { email, password });
        blocked.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // Read the code out of the email we just "sent".
        var emailMessage = Factory.EmailSender.SentMessages.Last(m => m.RecipientEmail == email);
        var code = TestCodeExtractor.ExtractSixDigitCode(emailMessage.TextBody);

        var verify = await client.PostAsJsonAsync("/auth/verify-email", new { email, code });
        verify.StatusCode.Should().Be(HttpStatusCode.OK);
        var verified = await verify.Content.ReadFromJsonAsync<AuthTokenResult>();
        verified!.AccessToken.Should().NotBeNullOrEmpty();

        // Now password login works.
        var login = await client.PostAsJsonAsync("/auth/login", new { email, password });
        login.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Test]
    public async Task VerifyEmail_WithWrongCode_IsUnauthorized()
    {
        var client = Factory.CreateClient();
        var email = UniqueEmail();
        await client.PostAsJsonAsync("/auth/register",
            new { email, password = "Password123!", displayName = "Bad" });

        var verify = await client.PostAsJsonAsync("/auth/verify-email", new { email, code = "000000" });
        verify.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
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

        await client.PostAsJsonAsync("/auth/register", new { email, password, displayName = "Refresh" });
        var emailMessage = Factory.EmailSender.SentMessages.Last(m => m.RecipientEmail == email);
        var code = TestCodeExtractor.ExtractSixDigitCode(emailMessage.TextBody);
        await client.PostAsJsonAsync("/auth/verify-email", new { email, code });

        // The verify response set the HttpOnly refresh cookie; refresh should rotate it.
        var refresh = await client.PostAsync("/auth/refresh", null);
        refresh.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private sealed record RegistrationResult(string Email, bool RequiresEmailVerification);
    private sealed record AuthTokenResult(string AccessToken, string UserId, string DisplayName, bool IsOnboardingCompleted, string Role);
}
