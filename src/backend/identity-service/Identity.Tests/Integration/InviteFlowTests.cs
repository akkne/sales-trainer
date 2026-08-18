using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Sellevate.BuildingBlocks.Tenancy;
using Sellevate.Identity.Features.Auth.Models;
using Sellevate.Identity.Features.Invites.Models;
using Sellevate.Identity.Features.Membership.Models;
using Sellevate.Identity.Infrastructure.Data;
using Sellevate.Identity.Tests.Helpers;

namespace Sellevate.Identity.Tests.Integration;

/// <summary>
/// Phase 40.7 — closed registration and invites. Runs against the real Postgres container, so the
/// tenant row-level-security policy created by the AddInvite migration is in force for every one of
/// these calls, not just the EF query filter.
/// </summary>
[TestFixture]
[Category("Integration")]
public class InviteFlowTests
{
    private TestWebApplicationFactory Factory => IntegrationTestSetup.Factory;

    private static string UniqueEmail() => $"invitee-{Guid.NewGuid():N}@test.com";

    [Test]
    public async Task CreateInvite_SingleEmail_ReturnsTokenOnceAndStoresOnlyItsHash()
    {
        var organizationId = Guid.NewGuid();
        var client = Factory.CreateOrganizationAdminClient(Guid.NewGuid(), organizationId);
        var email = UniqueEmail();

        var response = await client.PostAsJsonAsync("/invites", new { email, role = "Manager" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<CreateInvitesResponseDto>();
        body!.Created.Should().ContainSingle();
        var createdInvite = body.Created[0];
        createdInvite.Email.Should().Be(email);
        createdInvite.Token.Should().NotBeNullOrEmpty();

        await WithOrganizationScopeAsync(organizationId, async database =>
        {
            var storedInvite = await database.Invites.SingleAsync(invite => invite.Id == createdInvite.Id);
            storedInvite.TokenHash.Should().NotBe(createdInvite.Token);
            storedInvite.Email.Should().Be(email);
        });

        Factory.EmailSender.SentMessages.Should().Contain(message => message.RecipientEmail == email);
    }

    [Test]
    public async Task CreateInvites_BulkList_CreatesOnePerAddressAndReportsRejections()
    {
        var organizationId = Guid.NewGuid();
        var client = Factory.CreateOrganizationAdminClient(Guid.NewGuid(), organizationId);
        var firstEmail = UniqueEmail();
        var secondEmail = UniqueEmail();
        var thirdEmail = UniqueEmail();

        var response = await client.PostAsJsonAsync("/invites", new
        {
            emails = new[] { firstEmail, secondEmail, thirdEmail, firstEmail, "not-an-email" },
            role = "Manager"
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<CreateInvitesResponseDto>();
        body!.Created.Should().HaveCount(3);
        body.Created.Select(invite => invite.Email)
            .Should().BeEquivalentTo(new[] { firstEmail, secondEmail, thirdEmail });
        body.Created.Select(invite => invite.Token).Distinct().Should().HaveCount(3);
        body.Rejected.Should().HaveCount(2);
    }

    /// <summary>
    /// The caller is a fully entitled <c>TenancySuperAdmin</c>, so the 403 must come from the missing
    /// <c>X-Organization-Id</c> header alone and not from the role gate.
    /// </summary>
    [Test]
    public async Task CreateInvites_WithoutOrganizationHeader_IsForbidden()
    {
        var client = Factory.CreateAuthenticatedClient(
            Guid.NewGuid(), "orgadmin@test.com", "Org Admin", orgRole: "TenancySuperAdmin");

        var response = await client.PostAsJsonAsync("/invites", new { email = UniqueEmail(), role = "Manager" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task CreateInvites_AsPlainManager_IsForbidden()
    {
        var organizationId = Guid.NewGuid();
        var client = Factory.CreateAuthenticatedClient(
            Guid.NewGuid(), "manager@test.com", "Manager", organizationId: organizationId, orgRole: "Manager");
        client.DefaultRequestHeaders.Add("X-Organization-Id", organizationId.ToString());

        var response = await client.PostAsJsonAsync("/invites", new { email = UniqueEmail(), role = "Manager" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>
    /// The 2026-08-16 role split: adding and removing users is the single privilege that separates a
    /// tenancy admin from a tenancy superadmin. A <c>TenancyAdmin</c> runs everything else in the
    /// organization and still cannot invite.
    /// </summary>
    [Test]
    public async Task CreateInvites_AsTenancyAdmin_IsForbidden()
    {
        var organizationId = Guid.NewGuid();
        var client = Factory.CreateOrganizationAdminClient(
            Guid.NewGuid(), organizationId, organizationRole: "TenancyAdmin");

        var response = await client.PostAsJsonAsync("/invites", new { email = UniqueEmail(), role = "Manager" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>
    /// A platform <c>SuperAdmin</c> holds no membership anywhere, so it carries no <c>org_role</c>
    /// claim at all — the policy has to let it through on the platform role alone.
    /// </summary>
    [Test]
    public async Task CreateInvites_AsPlatformSuperAdminWithoutAnyMembership_IsAllowed()
    {
        var organizationId = Guid.NewGuid();
        var client = Factory.CreateAuthenticatedClient(
            Guid.NewGuid(), "platform@test.com", "Platform Staff", UserRole.SuperAdmin);
        client.DefaultRequestHeaders.Add("X-Organization-Id", organizationId.ToString());

        var response = await client.PostAsJsonAsync("/invites", new { email = UniqueEmail(), role = "Manager" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>
    /// The role name <c>OrgAdmin</c> was retired on 2026-08-16. A stale client must be told, not
    /// silently mapped onto whatever the closest surviving value happens to be.
    /// </summary>
    [Test]
    public async Task CreateInvites_WithTheRetiredOrgAdminRoleName_IsRejected()
    {
        var organizationId = Guid.NewGuid();
        var client = Factory.CreateOrganizationAdminClient(Guid.NewGuid(), organizationId);

        var response = await client.PostAsJsonAsync("/invites", new { email = UniqueEmail(), role = "OrgAdmin" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task AcceptInvite_NewEmail_CreatesUserWithMembershipAndOrgClaims()
    {
        var organizationId = Guid.NewGuid();
        var invitingUserId = Guid.NewGuid();
        var client = Factory.CreateOrganizationAdminClient(invitingUserId, organizationId);
        var email = UniqueEmail();
        var createdInvite = await CreateInviteAsync(client, email, "Manager");

        var anonymousClient = Factory.CreateClient();
        var acceptResponse = await anonymousClient.PostAsJsonAsync(
            $"/auth/invites/{createdInvite.Token}/accept",
            new { displayName = "Fresh Manager", password = "Password123!" });

        acceptResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var tokens = await acceptResponse.Content.ReadFromJsonAsync<AuthTokenResult>();
        tokens!.OrgId.Should().Be(organizationId.ToString());
        tokens.OrgRole.Should().Be("Manager");

        using var scope = Factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var user = await database.Users.SingleAsync(candidate => candidate.Email == email);
        user.IsEmailVerified.Should().BeTrue("the invite token already proves control of the mailbox");
        (await database.Memberships.CountAsync(membership => membership.UserId == user.Id)).Should().Be(1);
    }

    /// <summary>
    /// The case docs/TENANCY/TENANCY.md §4.3 calls impossible to retrofit: an invite for an address
    /// that already has an account must attach a second membership to that same account, never
    /// create a duplicate user.
    /// </summary>
    [Test]
    public async Task AcceptInvite_ExistingEmail_AddsMembershipAndCreatesNoSecondUser()
    {
        var firstOrganizationId = Guid.NewGuid();
        var secondOrganizationId = Guid.NewGuid();
        var email = UniqueEmail();
        var existingUser = await TestUserSeeder.SeedUserAsync(
            Factory, email, "Existing Person", organizationId: firstOrganizationId);

        var client = Factory.CreateOrganizationAdminClient(Guid.NewGuid(), secondOrganizationId);
        var createdInvite = await CreateInviteAsync(client, email, "TenancyAdmin");

        var anonymousClient = Factory.CreateClient();
        var acceptResponse = await anonymousClient.PostAsJsonAsync(
            $"/auth/invites/{createdInvite.Token}/accept",
            new { displayName = (string?)null, password = (string?)null });

        acceptResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var tokens = await acceptResponse.Content.ReadFromJsonAsync<AuthTokenResult>();
        tokens!.UserId.Should().Be(existingUser.Id.ToString());

        using var scope = Factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        (await database.Users.CountAsync(candidate => candidate.Email == email))
            .Should().Be(1, "an invite must never create a second account for a known address");

        var memberships = await database.Memberships
            .Where(membership => membership.UserId == existingUser.Id)
            .ToListAsync();
        memberships.Should().HaveCount(2);
        memberships.Should().Contain(membership =>
            membership.OrganizationId == secondOrganizationId
            && membership.Role == OrgRole.TenancyAdmin
            && membership.Status == MembershipStatus.Active);
    }

    [Test]
    public async Task AcceptInvite_ReusedAfterAcceptance_IsRejected()
    {
        var organizationId = Guid.NewGuid();
        var client = Factory.CreateOrganizationAdminClient(Guid.NewGuid(), organizationId);
        var createdInvite = await CreateInviteAsync(client, UniqueEmail(), "Manager");
        var anonymousClient = Factory.CreateClient();

        var firstAccept = await anonymousClient.PostAsJsonAsync(
            $"/auth/invites/{createdInvite.Token}/accept",
            new { displayName = "Once", password = "Password123!" });
        firstAccept.StatusCode.Should().Be(HttpStatusCode.OK);

        var secondAccept = await anonymousClient.PostAsJsonAsync(
            $"/auth/invites/{createdInvite.Token}/accept",
            new { displayName = "Twice", password = "Password123!" });

        secondAccept.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Test]
    public async Task AcceptInvite_Expired_IsRejected()
    {
        var organizationId = Guid.NewGuid();
        var client = Factory.CreateOrganizationAdminClient(Guid.NewGuid(), organizationId);
        var createdInvite = await CreateInviteAsync(client, UniqueEmail(), "Manager");

        await WithOrganizationScopeAsync(organizationId, async database =>
        {
            var invite = await database.Invites.SingleAsync(candidate => candidate.Id == createdInvite.Id);
            invite.ExpiresAt = DateTime.UtcNow.AddMinutes(-1);
        });

        var anonymousClient = Factory.CreateClient();
        var acceptResponse = await anonymousClient.PostAsJsonAsync(
            $"/auth/invites/{createdInvite.Token}/accept",
            new { displayName = "Too Late", password = "Password123!" });

        acceptResponse.StatusCode.Should().Be(HttpStatusCode.Gone);
    }

    [Test]
    public async Task AcceptInvite_AfterRevocation_IsRejected()
    {
        var organizationId = Guid.NewGuid();
        var client = Factory.CreateOrganizationAdminClient(Guid.NewGuid(), organizationId);
        var createdInvite = await CreateInviteAsync(client, UniqueEmail(), "Manager");

        var revokeResponse = await client.DeleteAsync($"/invites/{createdInvite.Id}");
        revokeResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var anonymousClient = Factory.CreateClient();
        var acceptResponse = await anonymousClient.PostAsJsonAsync(
            $"/auth/invites/{createdInvite.Token}/accept",
            new { displayName = "Revoked", password = "Password123!" });

        acceptResponse.StatusCode.Should().Be(HttpStatusCode.Gone);
    }

    /// <summary>
    /// Repoints the token at a different organization. The HMAC no longer matches, so the token must
    /// be refused before any lookup happens.
    /// </summary>
    [Test]
    public async Task AcceptInvite_WithTamperedToken_IsRejected()
    {
        var organizationId = Guid.NewGuid();
        var client = Factory.CreateOrganizationAdminClient(Guid.NewGuid(), organizationId);
        var createdInvite = await CreateInviteAsync(client, UniqueEmail(), "Manager");

        var tokenParts = createdInvite.Token.Split('.');
        var tamperedToken = $"{Guid.NewGuid():N}.{tokenParts[1]}.{tokenParts[2]}";

        var anonymousClient = Factory.CreateClient();
        var acceptResponse = await anonymousClient.PostAsJsonAsync(
            $"/auth/invites/{tamperedToken}/accept",
            new { displayName = "Impostor", password = "Password123!" });

        acceptResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task RevokeInvite_FromAnotherOrganization_IsNotFound()
    {
        var owningOrganizationId = Guid.NewGuid();
        var otherOrganizationId = Guid.NewGuid();
        var owningClient = Factory.CreateOrganizationAdminClient(Guid.NewGuid(), owningOrganizationId);
        var createdInvite = await CreateInviteAsync(owningClient, UniqueEmail(), "Manager");

        var otherClient = Factory.CreateOrganizationAdminClient(Guid.NewGuid(), otherOrganizationId);
        var revokeResponse = await otherClient.DeleteAsync($"/invites/{createdInvite.Id}");

        revokeResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var anonymousClient = Factory.CreateClient();
        var acceptResponse = await anonymousClient.PostAsJsonAsync(
            $"/auth/invites/{createdInvite.Token}/accept",
            new { displayName = "Still Valid", password = "Password123!" });
        acceptResponse.StatusCode.Should().Be(HttpStatusCode.OK, "the other organization must not have been able to revoke it");
    }

    [Test]
    public async Task DeactivateMembership_MarksItDeactivatedAndKeepsTheRow()
    {
        var organizationId = Guid.NewGuid();
        var member = await TestUserSeeder.SeedUserAsync(
            Factory, UniqueEmail(), "Leaver", organizationId: organizationId);
        var client = Factory.CreateOrganizationAdminClient(Guid.NewGuid(), organizationId);

        var response = await client.DeleteAsync($"/memberships/{member.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var scope = Factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var membership = await database.Memberships.SingleAsync(candidate =>
            candidate.UserId == member.Id && candidate.OrganizationId == organizationId);
        membership.Status.Should().Be(MembershipStatus.Deactivated);
        membership.DeactivatedAt.Should().NotBeNull();
    }

    [Test]
    public async Task DeactivateMembership_FromAnotherOrganization_IsNotFound()
    {
        var organizationId = Guid.NewGuid();
        var member = await TestUserSeeder.SeedUserAsync(
            Factory, UniqueEmail(), "Not Yours", organizationId: organizationId);
        var otherClient = Factory.CreateOrganizationAdminClient(Guid.NewGuid(), Guid.NewGuid());

        var response = await otherClient.DeleteAsync($"/memberships/{member.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private static async Task<CreatedInviteDto> CreateInviteAsync(HttpClient client, string email, string role)
    {
        var response = await client.PostAsJsonAsync("/invites", new { email, role });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<CreateInvitesResponseDto>();
        return body!.Created.Single();
    }

    /// <summary>
    /// Invites are behind row-level security, so a test that reads or edits one directly has to
    /// establish the tenant context and open an explicit transaction exactly like production code
    /// does — a bare SELECT would see nothing at all.
    /// </summary>
    private async Task WithOrganizationScopeAsync(Guid organizationId, Func<IdentityDbContext, Task> action)
    {
        using var scope = Factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().SetOrganization(organizationId);
        var database = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();

        await using var transaction = await database.Database.BeginTransactionAsync();
        await action(database);
        await database.SaveChangesAsync();
        await transaction.CommitAsync();
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
