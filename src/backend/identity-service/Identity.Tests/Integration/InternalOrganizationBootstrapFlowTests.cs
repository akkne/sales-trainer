using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Sellevate.BuildingBlocks.Tenancy;
using Sellevate.Identity.Features.Auth.Models;
using Sellevate.Identity.Features.Membership.Models;
using Sellevate.Identity.Infrastructure.Data;
using Sellevate.Identity.Tests.Helpers;

namespace Sellevate.Identity.Tests.Integration;

/// <summary>
/// <c>POST internal/organizations/{organizationId}/bootstrap-admin</c> — demo-request provisioning's
/// one call into identity-service. See <c>OrganizationBootstrapService</c> and docs/DECISIONS.md.
/// </summary>
[TestFixture]
[Category("Integration")]
public class InternalOrganizationBootstrapFlowTests
{
    private TestWebApplicationFactory Factory => IntegrationTestSetup.Factory;

    private static string UniqueEmail() => $"bootstrap-{Guid.NewGuid():N}@test.com";

    private static object BuildRequestBody(
        Guid actorUserId, string? email = null, string? role = null,
        string organizationName = "Fresh Customer", string organizationSlug = "fresh-customer")
        => new
        {
            organizationName,
            organizationSlug,
            email = email ?? UniqueEmail(),
            role,
            actorUserId,
        };

    private async Task<Guid> SeedSuperAdminActorAsync()
    {
        var actor = await TestUserSeeder.SeedUserAsync(
            Factory, UniqueEmail(), "Platform Staff", platformRole: UserRole.SuperAdmin);
        return actor.Id;
    }

    [Test]
    public async Task BootstrapAdministrator_WithNoSecretHeader_IsForbidden()
    {
        var organizationId = Guid.NewGuid();
        var actorUserId = await SeedSuperAdminActorAsync();
        var client = Factory.CreateInternalServiceClient(serviceSecret: null);

        var response = await client.PostAsJsonAsync(
            $"/internal/organizations/{organizationId}/bootstrap-admin", BuildRequestBody(actorUserId));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task BootstrapAdministrator_WithTheWrongSecret_IsForbidden()
    {
        var organizationId = Guid.NewGuid();
        var actorUserId = await SeedSuperAdminActorAsync();
        var client = Factory.CreateInternalServiceClient(serviceSecret: "not-the-configured-secret");

        var response = await client.PostAsJsonAsync(
            $"/internal/organizations/{organizationId}/bootstrap-admin", BuildRequestBody(actorUserId));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>
    /// The most valuable test in this file: without upserting the replica from the payload itself,
    /// this call would race organization-service's own Kafka consumer on every single provision, since
    /// it is issued in the same request that just committed the organization row.
    /// </summary>
    [Test]
    public async Task BootstrapAdministrator_WithNoOrganizationReplicaYet_StillSucceeds()
    {
        var organizationId = Guid.NewGuid();
        var actorUserId = await SeedSuperAdminActorAsync();
        var client = Factory.CreateInternalServiceClient();

        var response = await client.PostAsJsonAsync(
            $"/internal/organizations/{organizationId}/bootstrap-admin",
            BuildRequestBody(actorUserId, organizationName: "Never Replicated Yet", organizationSlug: "never-replicated-yet"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var scope = Factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var replica = await database.OrganizationReplicas.FindAsync(organizationId);
        replica.Should().NotBeNull();
        replica!.Name.Should().Be("Never Replicated Yet");
        replica.Slug.Should().Be("never-replicated-yet");
    }

    [Test]
    public async Task BootstrapAdministrator_WhenActorIsAPlatformAdminNotSuperAdmin_IsForbiddenWithNoInvite()
    {
        var organizationId = Guid.NewGuid();
        var actor = await TestUserSeeder.SeedUserAsync(
            Factory, UniqueEmail(), "Platform Admin", platformRole: UserRole.Admin);
        var client = Factory.CreateInternalServiceClient();

        var response = await client.PostAsJsonAsync(
            $"/internal/organizations/{organizationId}/bootstrap-admin", BuildRequestBody(actor.Id));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "the shared secret authorizes the channel, not the actor — a plain Admin must never be "
            + "laundered into a superadmin act");

        await WithOrganizationScopeAsync(organizationId, async database =>
            (await database.Invites.AnyAsync(invite => invite.OrganizationId == organizationId)).Should().BeFalse());
    }

    [Test]
    public async Task BootstrapAdministrator_WithAnUnknownActor_IsForbidden()
    {
        var organizationId = Guid.NewGuid();
        var client = Factory.CreateInternalServiceClient();

        var response = await client.PostAsJsonAsync(
            $"/internal/organizations/{organizationId}/bootstrap-admin", BuildRequestBody(Guid.NewGuid()));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task BootstrapAdministrator_WhenAnActiveAdministratorAlreadyExists_StillInvitesAnotherOne()
    {
        var organizationId = Guid.NewGuid();
        var actorUserId = await SeedSuperAdminActorAsync();
        await TestUserSeeder.SeedUserAsync(
            Factory, UniqueEmail(), "Sitting Admin",
            organizationId: organizationId, organizationRole: OrgRole.TenancySuperAdmin);
        var client = Factory.CreateInternalServiceClient();

        var response = await client.PostAsJsonAsync(
            $"/internal/organizations/{organizationId}/bootstrap-admin", BuildRequestBody(actorUserId));

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "an organization may have any number of administrators, so provisioning a lead into one "
            + "that already has some is not a conflict");

        await WithOrganizationScopeAsync(organizationId, async database =>
            (await database.Invites.CountAsync(candidate => candidate.OrganizationId == organizationId)).Should().Be(1));
    }

    /// <summary>
    /// The retry convergence is keyed on the invited address, not on the organization: a retry always
    /// arrives with the same <c>BootstrapAdminEmail</c> the first attempt used, while a call for a
    /// different address is a second administrator and must get its own invite.
    /// </summary>
    [Test]
    public async Task BootstrapAdministrator_WhenTheSameAddressIsAlreadyPending_ReturnsThatInvite()
    {
        var organizationId = Guid.NewGuid();
        var actorUserId = await SeedSuperAdminActorAsync();
        var client = Factory.CreateInternalServiceClient();
        var email = UniqueEmail();

        var firstResponse = await client.PostAsJsonAsync(
            $"/internal/organizations/{organizationId}/bootstrap-admin", BuildRequestBody(actorUserId, email: email));
        firstResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var firstInvite = await firstResponse.Content.ReadFromJsonAsync<BootstrapAdministratorResult>();

        var secondResponse = await client.PostAsJsonAsync(
            $"/internal/organizations/{organizationId}/bootstrap-admin", BuildRequestBody(actorUserId, email: email));

        secondResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            "a pending invite for this same address must converge a retry, not turn it into an error");
        var secondInvite = await secondResponse.Content.ReadFromJsonAsync<BootstrapAdministratorResult>();
        secondInvite!.InviteId.Should().Be(firstInvite!.InviteId);
        secondInvite.Email.Should().Be(email);
    }

    [Test]
    public async Task BootstrapAdministrator_WhenAnotherAddressIsAlreadyPending_MintsASecondInvite()
    {
        var organizationId = Guid.NewGuid();
        var actorUserId = await SeedSuperAdminActorAsync();
        var client = Factory.CreateInternalServiceClient();

        var firstResponse = await client.PostAsJsonAsync(
            $"/internal/organizations/{organizationId}/bootstrap-admin", BuildRequestBody(actorUserId));
        firstResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var firstInvite = await firstResponse.Content.ReadFromJsonAsync<BootstrapAdministratorResult>();

        var secondResponse = await client.PostAsJsonAsync(
            $"/internal/organizations/{organizationId}/bootstrap-admin", BuildRequestBody(actorUserId));

        secondResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var secondInvite = await secondResponse.Content.ReadFromJsonAsync<BootstrapAdministratorResult>();
        secondInvite!.InviteId.Should().NotBe(firstInvite!.InviteId,
            "a different address is a different administrator, and handing back somebody else's "
            + "invite would send the wrong person's link");

        await WithOrganizationScopeAsync(organizationId, async database =>
            (await database.Invites.CountAsync(candidate => candidate.OrganizationId == organizationId)).Should().Be(2));
    }

    [Test]
    public async Task BootstrapAdministrator_WithManagerRole_IsRejectedWithBadRequest()
    {
        var organizationId = Guid.NewGuid();
        var actorUserId = await SeedSuperAdminActorAsync();
        var client = Factory.CreateInternalServiceClient();

        var response = await client.PostAsJsonAsync(
            $"/internal/organizations/{organizationId}/bootstrap-admin", BuildRequestBody(actorUserId, role: "Manager"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task BootstrapAdministrator_WhenRoleIsOmitted_DefaultsToTenancySuperAdmin()
    {
        var organizationId = Guid.NewGuid();
        var actorUserId = await SeedSuperAdminActorAsync();
        var client = Factory.CreateInternalServiceClient();

        var response = await client.PostAsJsonAsync(
            $"/internal/organizations/{organizationId}/bootstrap-admin", BuildRequestBody(actorUserId));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        await WithOrganizationScopeAsync(organizationId, async database =>
        {
            var invite = await database.Invites.SingleAsync(candidate => candidate.OrganizationId == organizationId);
            invite.Role.Should().Be(OrgRole.TenancySuperAdmin);
        });
    }

    [Test]
    public async Task BootstrapAdministrator_RecordsInvitedByAsTheActor()
    {
        var organizationId = Guid.NewGuid();
        var actorUserId = await SeedSuperAdminActorAsync();
        var client = Factory.CreateInternalServiceClient();

        var response = await client.PostAsJsonAsync(
            $"/internal/organizations/{organizationId}/bootstrap-admin", BuildRequestBody(actorUserId));
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        await WithOrganizationScopeAsync(organizationId, async database =>
        {
            var invite = await database.Invites.SingleAsync(candidate => candidate.OrganizationId == organizationId);
            invite.InvitedBy.Should().Be(actorUserId);
        });
    }

    [Test]
    public async Task BootstrapAdministrator_WhenTheInviteMailThrowsAfterCommit_StillReturns200()
    {
        var organizationId = Guid.NewGuid();
        var actorUserId = await SeedSuperAdminActorAsync();
        var client = Factory.CreateInternalServiceClient();
        Factory.EmailSender.ExceptionToThrowOnNextSend = new InvalidOperationException("MailerSend is down.");

        var response = await client.PostAsJsonAsync(
            $"/internal/organizations/{organizationId}/bootstrap-admin", BuildRequestBody(actorUserId));

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "a committed invite must never surface as a 500 just because the mail send after it failed");

        await WithOrganizationScopeAsync(organizationId, async database =>
            (await database.Invites.CountAsync(candidate => candidate.OrganizationId == organizationId)).Should().Be(1));
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

    private sealed record BootstrapAdministratorResult(Guid InviteId, string Email, DateTime ExpiresAt);
}
