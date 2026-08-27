using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Sellevate.BuildingBlocks.Tenancy;
using Sellevate.Identity.Features.Auth.Models;
using Sellevate.Identity.Features.Membership.Models;
using Sellevate.Identity.Features.Organizations.Models;
using Sellevate.Identity.Features.PlatformAdmin.Constants;
using Sellevate.Identity.Infrastructure.Data;
using Sellevate.Identity.Tests.Helpers;

namespace Sellevate.Identity.Tests.Integration;

/// <summary>
/// Phase 40.9 — the platform superadmin surface: impersonation and inviting an organization's
/// administrators, at whichever of <c>TenancyAdmin</c> or <c>TenancySuperAdmin</c> the request chose
/// (2026-08-20). There is no limit on how many an organization may have (2026-08-27).
/// </summary>
[TestFixture]
[Category("Integration")]
public class PlatformAdminTests
{
    /// <summary>
    /// <c>JwtSecurityTokenHandler</c> writes the short claim name into the token; the long
    /// <c>ClaimTypes.Role</c> URI only appears after inbound claim mapping on the reading side, so a
    /// token inspected as written carries this spelling.
    /// </summary>
    private const string ShortRoleClaimType = "role";

    private TestWebApplicationFactory Factory => IntegrationTestSetup.Factory;

    private static string UniqueEmail() => $"platform-{Guid.NewGuid():N}@test.com";

    private HttpClient CreateSuperAdminClient(Guid? userId = null, string? email = null) =>
        Factory.CreateAuthenticatedClient(
            userId ?? Guid.NewGuid(),
            email ?? "superadmin@test.com",
            "Platform Staff",
            UserRole.SuperAdmin);

    [Test]
    public async Task StartImpersonation_AsOrdinaryUser_IsForbidden()
    {
        var organizationId = Guid.NewGuid();
        await TestOrganizationSeeder.SeedOrganizationAsync(Factory, organizationId);
        var client = Factory.CreateAuthenticatedClient(
            Guid.NewGuid(), UniqueEmail(), "Ordinary", UserRole.User, organizationId, "TenancyAdmin");

        var response = await client.PostAsJsonAsync(
            "/admin/platform/impersonation",
            new { organizationId, reason = "curiosity" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task ListImpersonations_AsOrdinaryUser_IsForbidden()
    {
        var client = Factory.CreateAuthenticatedClient(Guid.NewGuid(), UniqueEmail(), "Ordinary");

        var response = await client.GetAsync("/admin/platform/impersonation");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task BootstrapOrganizationAdmin_AsOrdinaryUser_IsForbidden()
    {
        var organizationId = Guid.NewGuid();
        await TestOrganizationSeeder.SeedOrganizationAsync(Factory, organizationId);
        var client = Factory.CreateAuthenticatedClient(
            Guid.NewGuid(), UniqueEmail(), "Ordinary", UserRole.User, organizationId, "TenancyAdmin");

        var response = await client.PostAsJsonAsync(
            "/admin/platform/organizations/bootstrap-admin",
            new { organizationId, email = UniqueEmail() });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task StartImpersonation_ForUnknownOrganization_IsNotFound()
    {
        var client = CreateSuperAdminClient();

        var response = await client.PostAsJsonAsync(
            "/admin/platform/impersonation",
            new { organizationId = Guid.NewGuid(), reason = "support ticket 12" });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task StartImpersonation_MintsAShortLivedNonEscalatingTokenAndAuditsIt()
    {
        var organizationId = Guid.NewGuid();
        await TestOrganizationSeeder.SeedOrganizationAsync(Factory, organizationId, "Acme Sales");
        var actorUserId = Guid.NewGuid();
        var actorEmail = UniqueEmail();
        var client = CreateSuperAdminClient(actorUserId, actorEmail);

        var response = await client.PostAsJsonAsync(
            "/admin/platform/impersonation",
            new { organizationId, reason = "support ticket 42" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var issuedToken = await response.Content.ReadFromJsonAsync<ImpersonationTokenResult>();
        issuedToken!.Organization.Id.Should().Be(organizationId);
        issuedToken.Organization.Name.Should().Be("Acme Sales");

        var parsedToken = new JwtSecurityTokenHandler().ReadJwtToken(issuedToken.AccessToken);

        parsedToken.Claims.Should().Contain(claim =>
            claim.Type == "org_id" && claim.Value == organizationId.ToString());
        parsedToken.Claims.Should().Contain(claim =>
            claim.Type == ImpersonationClaimNames.IsImpersonation && claim.Value == "true");
        parsedToken.Claims.Should().Contain(claim =>
            claim.Type == ImpersonationClaimNames.ImpersonationId
            && claim.Value == issuedToken.ImpersonationId.ToString());
        parsedToken.Claims.Should().Contain(claim =>
            claim.Type == ImpersonationClaimNames.ActorUserId && claim.Value == actorUserId.ToString());

        parsedToken.Claims.Should().Contain(claim =>
            claim.Type == ShortRoleClaimType && claim.Value == nameof(UserRole.User));
        parsedToken.Claims.Should().NotContain(claim => claim.Value == nameof(UserRole.SuperAdmin));

        parsedToken.ValidTo.Should().BeBefore(DateTime.UtcNow.AddHours(1));

        using var scope = Factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var auditEntry = await database.ImpersonationAuditEntries
            .SingleAsync(entry => entry.Id == issuedToken.ImpersonationId);
        auditEntry.ActorUserId.Should().Be(actorUserId);
        auditEntry.ActorEmail.Should().Be(actorEmail);
        auditEntry.OrganizationId.Should().Be(organizationId);
        auditEntry.Reason.Should().Be("support ticket 42");
    }

    [Test]
    public async Task StartImpersonation_AppearsInTheAuditList()
    {
        var organizationId = Guid.NewGuid();
        await TestOrganizationSeeder.SeedOrganizationAsync(Factory, organizationId, "Audited Org");
        var client = CreateSuperAdminClient();

        var startResponse = await client.PostAsJsonAsync(
            "/admin/platform/impersonation",
            new { organizationId, reason = "escalation review" });
        var issuedToken = await startResponse.Content.ReadFromJsonAsync<ImpersonationTokenResult>();

        var auditResponse = await client.GetAsync("/admin/platform/impersonation");

        auditResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var auditEntries = await auditResponse.Content.ReadFromJsonAsync<List<ImpersonationAuditResult>>();
        auditEntries!.Should().Contain(entry =>
            entry.Id == issuedToken!.ImpersonationId
            && entry.Organization.Id == organizationId
            && entry.Reason == "escalation review");
    }

    [Test]
    public async Task ImpersonationToken_CannotStartAnotherImpersonation()
    {
        var firstOrganizationId = Guid.NewGuid();
        var secondOrganizationId = Guid.NewGuid();
        await TestOrganizationSeeder.SeedOrganizationAsync(Factory, firstOrganizationId);
        await TestOrganizationSeeder.SeedOrganizationAsync(Factory, secondOrganizationId);
        var client = CreateSuperAdminClient();

        var startResponse = await client.PostAsJsonAsync(
            "/admin/platform/impersonation",
            new { organizationId = firstOrganizationId, reason = "first hop" });
        var issuedToken = await startResponse.Content.ReadFromJsonAsync<ImpersonationTokenResult>();

        var impersonatingClient = Factory.CreateClient();
        impersonatingClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", issuedToken!.AccessToken);

        var chainedResponse = await impersonatingClient.PostAsJsonAsync(
            "/admin/platform/impersonation",
            new { organizationId = secondOrganizationId, reason = "second hop" });

        chainedResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task StartImpersonation_IntoSuspendedOrganization_IsForbidden()
    {
        var organizationId = Guid.NewGuid();
        await TestOrganizationSeeder.SeedOrganizationAsync(
            Factory, organizationId, status: OrganizationReplicaStatus.Suspended);
        var client = CreateSuperAdminClient();

        var response = await client.PostAsJsonAsync(
            "/admin/platform/impersonation",
            new { organizationId, reason = "let me in anyway" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [TestCase(OrgRole.TenancySuperAdmin)]
    [TestCase(OrgRole.TenancyAdmin)]
    public async Task BootstrapOrganizationAdmin_CreatesAnInviteAtTheChosenRoleThatCanBeAccepted(OrgRole requestedRole)
    {
        var organizationId = Guid.NewGuid();
        await TestOrganizationSeeder.SeedOrganizationAsync(Factory, organizationId, "Fresh Customer");
        var client = CreateSuperAdminClient();
        var inviteeEmail = UniqueEmail();

        var response = await client.PostAsJsonAsync(
            "/admin/platform/organizations/bootstrap-admin",
            new { organizationId, email = inviteeEmail, role = requestedRole.ToString() });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var bootstrapInvite = await response.Content.ReadFromJsonAsync<BootstrapInviteResult>();
        bootstrapInvite!.Email.Should().Be(inviteeEmail);
        bootstrapInvite.Organization.Id.Should().Be(organizationId);
        bootstrapInvite.Token.Should().NotBeNullOrEmpty();
        Factory.EmailSender.SentMessages.Should().Contain(message => message.RecipientEmail == inviteeEmail);

        var anonymousClient = Factory.CreateClient();
        var acceptResponse = await anonymousClient.PostAsJsonAsync(
            $"/auth/invites/{bootstrapInvite.Token}/accept",
            new { displayName = "First Admin", password = "Password123!" });

        acceptResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        using var scope = Factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var membership = await database.Memberships
            .SingleAsync(candidate => candidate.OrganizationId == organizationId);
        membership.Role.Should().Be(requestedRole,
            "the invite must be created and accepted at exactly the role the request chose, not a "
            + "hardcoded one");
        membership.Status.Should().Be(MembershipStatus.Active);
    }

    [Test]
    public async Task BootstrapOrganizationAdmin_WhenRoleIsOmitted_DefaultsToTenancySuperAdmin()
    {
        var organizationId = Guid.NewGuid();
        await TestOrganizationSeeder.SeedOrganizationAsync(Factory, organizationId);
        var client = CreateSuperAdminClient();
        var inviteeEmail = UniqueEmail();

        var response = await client.PostAsJsonAsync(
            "/admin/platform/organizations/bootstrap-admin",
            new { organizationId, email = inviteeEmail });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        await WithOrganizationScopeAsync(organizationId, async database =>
        {
            var invite = await database.Invites
                .SingleAsync(candidate => candidate.OrganizationId == organizationId);
            invite.Role.Should().Be(OrgRole.TenancySuperAdmin,
                "every caller that predates the role field sends none at all and must keep getting "
                + "exactly what it always got");
        });
    }

    [Test]
    public async Task BootstrapOrganizationAdmin_WithManagerRole_IsRejectedWithBadRequest()
    {
        var organizationId = Guid.NewGuid();
        await TestOrganizationSeeder.SeedOrganizationAsync(Factory, organizationId);
        var client = CreateSuperAdminClient();

        var response = await client.PostAsJsonAsync(
            "/admin/platform/organizations/bootstrap-admin",
            new { organizationId, email = UniqueEmail(), role = "Manager" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "this platform endpoint may only choose which rank of administrator it bootstraps, "
            + "never mint an ordinary organization role");
    }

    [Test]
    public async Task BootstrapOrganizationAdmin_WithAnUnknownRole_IsRejectedWithBadRequest()
    {
        var organizationId = Guid.NewGuid();
        await TestOrganizationSeeder.SeedOrganizationAsync(Factory, organizationId);
        var client = CreateSuperAdminClient();

        var response = await client.PostAsJsonAsync(
            "/admin/platform/organizations/bootstrap-admin",
            new { organizationId, email = UniqueEmail(), role = "Wizard" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [TestCase(OrgRole.TenancySuperAdmin)]
    [TestCase(OrgRole.TenancyAdmin)]
    public async Task BootstrapOrganizationAdmin_WhenAnInviteIsAlreadyPending_InvitesTheSecondAddressToo(
        OrgRole pendingInviteRole)
    {
        var organizationId = Guid.NewGuid();
        await TestOrganizationSeeder.SeedOrganizationAsync(Factory, organizationId);
        var client = CreateSuperAdminClient();

        var firstResponse = await client.PostAsJsonAsync(
            "/admin/platform/organizations/bootstrap-admin",
            new { organizationId, email = UniqueEmail(), role = pendingInviteRole.ToString() });
        firstResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var secondResponse = await client.PostAsJsonAsync(
            "/admin/platform/organizations/bootstrap-admin",
            new { organizationId, email = UniqueEmail() });

        secondResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            "an organization may have as many administrators as it needs — a pending invite for "
            + "somebody else is not a reason to refuse the next one");

        await WithOrganizationScopeAsync(organizationId, async database =>
            (await database.Invites.CountAsync(candidate => candidate.OrganizationId == organizationId))
                .Should().Be(2));
    }

    [Test]
    public async Task BootstrapOrganizationAdmin_ForAnAddressThatAlreadyHasAPendingInvite_IsBadRequest()
    {
        var organizationId = Guid.NewGuid();
        await TestOrganizationSeeder.SeedOrganizationAsync(Factory, organizationId);
        var client = CreateSuperAdminClient();
        var email = UniqueEmail();

        var firstResponse = await client.PostAsJsonAsync(
            "/admin/platform/organizations/bootstrap-admin", new { organizationId, email });
        firstResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var secondResponse = await client.PostAsJsonAsync(
            "/admin/platform/organizations/bootstrap-admin", new { organizationId, email });

        secondResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "dropping the one-administrator cap does not drop the ordinary invite rules — one "
            + "address still cannot hold two live invites into the same organization");
    }

    [TestCase(OrgRole.TenancySuperAdmin)]
    [TestCase(OrgRole.TenancyAdmin)]
    public async Task BootstrapOrganizationAdmin_WhenAnAdministratorAlreadyExists_InvitesAnotherOne(
        OrgRole existingAdministratorRole)
    {
        var organizationId = Guid.NewGuid();
        await TestOrganizationSeeder.SeedOrganizationAsync(Factory, organizationId);
        await TestUserSeeder.SeedUserAsync(
            Factory, UniqueEmail(), "Sitting Admin",
            organizationId: organizationId, organizationRole: existingAdministratorRole);
        var client = CreateSuperAdminClient();

        var response = await client.PostAsJsonAsync(
            "/admin/platform/organizations/bootstrap-admin",
            new { organizationId, email = UniqueEmail() });

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "a company with two РОПs is ordinary, and staffing the second one must not need a "
            + "support ticket");
    }

    [TestCase(OrgRole.TenancySuperAdmin)]
    [TestCase(OrgRole.TenancyAdmin)]
    public async Task BootstrapOrganizationAdmin_AfterTheFirstAdministratorAcceptsTheInvite_ASecondBootstrapSucceeds(
        OrgRole firstAdministratorRole)
    {
        var organizationId = Guid.NewGuid();
        await TestOrganizationSeeder.SeedOrganizationAsync(Factory, organizationId);
        var client = CreateSuperAdminClient();

        var firstResponse = await client.PostAsJsonAsync(
            "/admin/platform/organizations/bootstrap-admin",
            new { organizationId, email = UniqueEmail(), role = firstAdministratorRole.ToString() });
        firstResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var bootstrapInvite = await firstResponse.Content.ReadFromJsonAsync<BootstrapInviteResult>();

        var anonymousClient = Factory.CreateClient();
        var acceptResponse = await anonymousClient.PostAsJsonAsync(
            $"/auth/invites/{bootstrapInvite!.Token}/accept",
            new { displayName = "First Admin", password = "Password123!" });
        acceptResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var secondResponse = await client.PostAsJsonAsync(
            "/admin/platform/organizations/bootstrap-admin",
            new { organizationId, email = UniqueEmail() });

        secondResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            $"an organization whose administrator accepted at {firstAdministratorRole} may still be "
            + "given another one");

        var sameAddressResponse = await client.PostAsJsonAsync(
            "/admin/platform/organizations/bootstrap-admin",
            new { organizationId, email = bootstrapInvite.Email });

        sameAddressResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "the person who already accepted is already a member, which the ordinary invite rules "
            + "refuse");
    }

    [Test]
    public async Task BootstrapOrganizationAdmin_ForSuspendedOrganization_IsForbidden()
    {
        var organizationId = Guid.NewGuid();
        await TestOrganizationSeeder.SeedOrganizationAsync(
            Factory, organizationId, status: OrganizationReplicaStatus.Suspended);
        var client = CreateSuperAdminClient();

        var response = await client.PostAsJsonAsync(
            "/admin/platform/organizations/bootstrap-admin",
            new { organizationId, email = UniqueEmail() });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>
    /// `Invites` is tenant-scoped and row-level security's `SET LOCAL app.organization_id` only fires
    /// inside a transaction, so a read from a bare scope answers "no invites" for an organization that
    /// has some. Same helper, same reason, as `InviteFlowTests`.
    /// </summary>
    private async Task WithOrganizationScopeAsync(Guid organizationId, Func<IdentityDbContext, Task> action)
    {
        using var scope = Factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().SetOrganization(organizationId);
        var database = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();

        await using var transaction = await database.Database.BeginTransactionAsync();
        await action(database);
        await transaction.CommitAsync();
    }

    private sealed record OrganizationReferenceResult(Guid Id, string Name);

    private sealed record ImpersonationTokenResult(
        string AccessToken,
        DateTime ExpiresAt,
        Guid ImpersonationId,
        OrganizationReferenceResult Organization);

    private sealed record ImpersonationAuditResult(
        Guid Id,
        Guid ActorUserId,
        string ActorEmail,
        OrganizationReferenceResult Organization,
        string Reason,
        DateTime IssuedAt,
        DateTime ExpiresAt);

    private sealed record BootstrapInviteResult(
        Guid InviteId,
        OrganizationReferenceResult Organization,
        string Email,
        DateTime ExpiresAt,
        string Token);
}
