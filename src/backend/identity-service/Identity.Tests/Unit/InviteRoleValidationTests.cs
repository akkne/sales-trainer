using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using NUnit.Framework;
using Sellevate.BuildingBlocks.Email.Abstract;
using Sellevate.BuildingBlocks.Tenancy;
using Sellevate.Identity.Eventing;
using Sellevate.Identity.Features.Auth.Services.Abstract;
using Sellevate.Identity.Features.Invites.Models;
using Sellevate.Identity.Features.Invites.Services.Abstract;
using Sellevate.Identity.Features.Invites.Services.Implementation;
using Sellevate.Identity.Features.Membership.Models;
using Sellevate.Identity.Infrastructure.Configuration;

namespace Sellevate.Identity.Tests.Unit;

/// <summary>
/// The invite role arrives as free text from an HTTP body, so it is the one place where the
/// 2026-08-16 rename can be re-introduced by a stale caller. The controller turns
/// <see cref="ArgumentException"/> into a 400, so what these assert is exactly what the API
/// answers.
/// </summary>
[TestFixture]
public sealed class InviteRoleValidationTests
{
    private const string AnyInviteeEmail = "invitee@test.com";

    private static InviteService BuildService(out TenantContext tenantContext)
    {
        var scopedTenantContext = new TenantContext();
        scopedTenantContext.SetOrganization(Guid.NewGuid());
        tenantContext = scopedTenantContext;

        var databaseContext = Helpers.InMemoryDbContextFactory.Create(tenantContext: scopedTenantContext);

        return new InviteService(
            databaseContext,
            scopedTenantContext,
            Substitute.For<IInviteTokenFactory>(),
            Substitute.For<IAuthenticationService>(),
            Substitute.For<IUserEventPublisher>(),
            Substitute.For<IEmailSender>(),
            Options.Create(new InviteConfiguration()),
            NullLogger<InviteService>.Instance);
    }

    private static Func<Task> CreatingAnInviteWithRole(string role)
    {
        var inviteService = BuildService(out _);
        return async () => await inviteService.CreateAsync(
            new CreateInvitesRequestDto(AnyInviteeEmail, Emails: null, Role: role),
            Guid.NewGuid());
    }

    [Test]
    public async Task The_retired_OrgAdmin_name_is_rejected_with_a_message_naming_its_replacement()
    {
        var exception = await CreatingAnInviteWithRole("OrgAdmin")
            .Should().ThrowAsync<ArgumentException>();

        exception.Which.Message.Should().Contain(nameof(OrgRole.TenancyAdmin));
        exception.Which.Message.Should().Contain(nameof(OrgRole.TenancySuperAdmin));
    }

    [Test]
    public async Task The_retired_name_is_rejected_case_insensitively()
    {
        await CreatingAnInviteWithRole("orgadmin").Should().ThrowAsync<ArgumentException>();
    }

    [Test]
    public async Task An_unknown_role_name_is_rejected()
    {
        await CreatingAnInviteWithRole("Wizard").Should().ThrowAsync<ArgumentException>();
    }

    [Test]
    public async Task A_numeric_role_outside_the_enum_is_rejected()
    {
        await CreatingAnInviteWithRole("99").Should().ThrowAsync<ArgumentException>(
            "Enum.TryParse accepts bare numbers and would otherwise persist an undefined role");
    }

    [TestCase("Manager")]
    [TestCase("TenancyAdmin")]
    [TestCase("TenancySuperAdmin")]
    [TestCase("tenancysuperadmin")]
    public async Task Every_current_role_name_is_accepted(string role)
    {
        await CreatingAnInviteWithRole(role).Should().NotThrowAsync<ArgumentException>();
    }
}
