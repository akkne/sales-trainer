using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using NUnit.Framework;
using Sellevate.Identity.Features.Organizations.Models;
using Sellevate.Identity.Tests.Helpers;

namespace Sellevate.Identity.Tests.Integration;

/// <summary>
/// Phase 40.9 — suspending an organization has to stop its people signing in, not merely flip a
/// flag on a screen. Every entry route converges on the same token-issuing method, so the checks
/// below cover password login, refresh and invite acceptance through that one point.
/// </summary>
[TestFixture]
[Category("Integration")]
public class OrganizationSuspensionTests
{
    private TestWebApplicationFactory Factory => IntegrationTestSetup.Factory;

    private static string UniqueEmail() => $"suspension-{Guid.NewGuid():N}@test.com";

    [Test]
    public async Task Login_WhileOrganizationIsSuspended_IsForbidden()
    {
        var organizationId = Guid.NewGuid();
        await TestOrganizationSeeder.SeedOrganizationAsync(
            Factory, organizationId, status: OrganizationReplicaStatus.Suspended);
        var email = UniqueEmail();
        await TestUserSeeder.SeedUserAsync(Factory, email, "Suspended Member", organizationId: organizationId);

        var anonymousClient = Factory.CreateClient();
        var response = await anonymousClient.PostAsJsonAsync(
            "/auth/login", new { email, password = TestUserSeeder.DefaultPassword });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task Login_AfterTheOrganizationIsReactivated_Succeeds()
    {
        var organizationId = Guid.NewGuid();
        await TestOrganizationSeeder.SeedOrganizationAsync(
            Factory, organizationId, status: OrganizationReplicaStatus.Suspended);
        var email = UniqueEmail();
        await TestUserSeeder.SeedUserAsync(Factory, email, "Returning Member", organizationId: organizationId);

        var anonymousClient = Factory.CreateClient();
        var whileSuspended = await anonymousClient.PostAsJsonAsync(
            "/auth/login", new { email, password = TestUserSeeder.DefaultPassword });
        whileSuspended.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        await TestOrganizationSeeder.SetStatusAsync(Factory, organizationId, OrganizationReplicaStatus.Active);

        var afterReactivation = await anonymousClient.PostAsJsonAsync(
            "/auth/login", new { email, password = TestUserSeeder.DefaultPassword });

        afterReactivation.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Test]
    public async Task RefreshToken_StopsWorkingOnceTheOrganizationIsSuspended()
    {
        var organizationId = Guid.NewGuid();
        await TestOrganizationSeeder.SeedOrganizationAsync(Factory, organizationId);
        var email = UniqueEmail();
        await TestUserSeeder.SeedUserAsync(Factory, email, "Session Holder", organizationId: organizationId);

        var client = Factory.CreateClient();
        var loginResponse = await client.PostAsJsonAsync(
            "/auth/login", new { email, password = TestUserSeeder.DefaultPassword });
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var refreshToken = ExtractRefreshTokenCookie(loginResponse);

        await TestOrganizationSeeder.SetStatusAsync(Factory, organizationId, OrganizationReplicaStatus.Suspended);

        var refreshRequest = new HttpRequestMessage(HttpMethod.Post, "/auth/refresh");
        refreshRequest.Headers.Add("Cookie", $"refreshToken={refreshToken}");
        var refreshResponse = await client.SendAsync(refreshRequest);

        refreshResponse.StatusCode.Should().Be(
            HttpStatusCode.Forbidden,
            "an already-issued session must not outlive the suspension by the refresh token's lifetime");
    }

    [Test]
    public async Task AcceptInvite_IntoASuspendedOrganization_IsForbidden()
    {
        var organizationId = Guid.NewGuid();
        await TestOrganizationSeeder.SeedOrganizationAsync(Factory, organizationId);
        var organizationAdminClient = Factory.CreateOrganizationAdminClient(Guid.NewGuid(), organizationId);
        var inviteResponse = await organizationAdminClient.PostAsJsonAsync(
            "/invites", new { email = UniqueEmail(), role = "Manager" });
        inviteResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var inviteToken = await ExtractInviteTokenAsync(inviteResponse);

        await TestOrganizationSeeder.SetStatusAsync(Factory, organizationId, OrganizationReplicaStatus.Suspended);

        var anonymousClient = Factory.CreateClient();
        var acceptResponse = await anonymousClient.PostAsJsonAsync(
            $"/auth/invites/{inviteToken}/accept",
            new { displayName = "Late Arrival", password = "Password123!" });

        acceptResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private static string ExtractRefreshTokenCookie(HttpResponseMessage response)
    {
        var setCookieHeader = response.Headers.GetValues("Set-Cookie")
            .First(value => value.StartsWith("refreshToken="));
        return setCookieHeader["refreshToken=".Length..setCookieHeader.IndexOf(';')];
    }

    private static async Task<string> ExtractInviteTokenAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadFromJsonAsync<CreatedInvitesResult>();
        return body!.Created.Single().Token;
    }

    private sealed record CreatedInviteResult(string Token);

    private sealed record CreatedInvitesResult(List<CreatedInviteResult> Created);

    [Test]
    public async Task Login_WhenTheOrganizationIsNotYetReplicated_StillSucceeds()
    {
        var organizationId = Guid.NewGuid();
        var email = UniqueEmail();
        await TestUserSeeder.SeedUserAsync(Factory, email, "Unreplicated Member", organizationId: organizationId);

        var anonymousClient = Factory.CreateClient();
        var response = await anonymousClient.PostAsJsonAsync(
            "/auth/login", new { email, password = TestUserSeeder.DefaultPassword });

        response.StatusCode.Should().Be(
            HttpStatusCode.OK,
            "a replica that has not caught up must never read as a suspension");
    }
}
