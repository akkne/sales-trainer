using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using Sellevate.BuildingBlocks.Tenancy;
using Sellevate.Identity.Features.Auth.Models;
using Sellevate.Identity.Features.Membership.Endpoints;
using Sellevate.Identity.Features.Membership.Models;
using Sellevate.Identity.Infrastructure.Data;
using Sellevate.Identity.Tests.Helpers;

namespace Sellevate.Identity.Tests.Unit;

/// <summary>
/// <c>GET /memberships</c> — the roster the organization's own admin panel is built on (Phase 40.20).
///
/// <para>
/// Two properties here are security properties rather than behaviour: the query must start from
/// <c>Memberships</c> and never enumerate the platform-global <c>Users</c> table, and it must return
/// nothing at all when the request carries no organization. The third — that an offboarded person is
/// absent by default — is the difference between a list of people to assign work to and a list of
/// everyone who ever worked here.
/// </para>
/// </summary>
[TestFixture]
public sealed class MembershipsControllerListTests
{
    private static readonly Guid CallingOrganizationId = Guid.Parse("aaaaaaaa-0000-4000-8000-000000000011");
    private static readonly Guid OtherOrganizationId = Guid.Parse("bbbbbbbb-0000-4000-8000-000000000022");

    private IdentityDbContext _databaseContext = null!;
    private TenantContext _tenantContext = null!;
    private MembershipsController _controller = null!;

    [SetUp]
    public void SetUp()
    {
        _tenantContext = new TenantContext();
        _tenantContext.SetOrganization(CallingOrganizationId);
        _databaseContext = InMemoryDbContextFactory.Create(
            databaseName: $"memberships-list-{Guid.NewGuid()}", _tenantContext);
        _controller = new MembershipsController(
            _databaseContext, _tenantContext, NullLogger<MembershipsController>.Instance);
    }

    [TearDown]
    public void TearDown() => _databaseContext.Dispose();

    [Test]
    public async Task Active_is_the_default_and_leaves_out_the_offboarded()
    {
        await SeedAsync(
            (Person("anna@example.com", "Анна"), Member(MembershipStatus.Active)),
            (Person("boris@example.com", "Борис"), Member(MembershipStatus.Deactivated)));

        var roster = await ListAsync();

        roster.Should().ContainSingle().Which.Email.Should().Be("anna@example.com");
    }

    [Test]
    public async Task Deactivated_and_all_are_reachable_on_request()
    {
        await SeedAsync(
            (Person("anna@example.com", "Анна"), Member(MembershipStatus.Active)),
            (Person("boris@example.com", "Борис"), Member(MembershipStatus.Deactivated)));

        (await ListAsync(MembershipStatusFilter.Deactivated))
            .Should().ContainSingle().Which.Email.Should().Be("boris@example.com");
        (await ListAsync(MembershipStatusFilter.All)).Should().HaveCount(2);
    }

    [Test]
    public async Task Another_organizations_people_are_never_returned()
    {
        await SeedAsync(
            (Person("ours@example.com", "Наш"), Member(MembershipStatus.Active)),
            (Person("theirs@example.com", "Чужой"), Member(MembershipStatus.Active, OtherOrganizationId)));

        var roster = await ListAsync(MembershipStatusFilter.All);

        roster.Should().ContainSingle().Which.Email.Should().Be("ours@example.com");
    }

    /// <summary>
    /// A user with no membership row is not part of any organization, and a roster that joined from
    /// the other side would list them for whoever asked first. The join direction is the boundary.
    /// </summary>
    [Test]
    public async Task A_user_without_a_membership_is_invisible()
    {
        _databaseContext.Users.Add(Person("orphan@example.com", "Ничей"));
        await _databaseContext.SaveChangesAsync();

        (await ListAsync(MembershipStatusFilter.All)).Should().BeEmpty();
    }

    [Test]
    public async Task Without_an_organization_in_context_the_roster_is_refused_rather_than_widened()
    {
        await SeedAsync((Person("anna@example.com", "Анна"), Member(MembershipStatus.Active)));
        var contextWithoutOrganization = new TenantContext();
        var controller = new MembershipsController(
            _databaseContext, contextWithoutOrganization, NullLogger<MembershipsController>.Instance);

        var result = await controller.ListMemberships(MembershipStatusFilter.All);

        result.Result.Should().BeOfType<ForbidResult>();
    }

    [Test]
    public async Task The_role_and_status_travel_as_names_rather_than_numbers()
    {
        await SeedAsync(
            (Person("rop@example.com", "РОП"), Member(MembershipStatus.Active, role: OrgRole.TenancyAdmin)));

        var row = (await ListAsync()).Single();

        row.Role.Should().Be("TenancyAdmin");
        row.Status.Should().Be("Active");
    }

    private async Task<IReadOnlyList<MembershipDto>> ListAsync(
        MembershipStatusFilter status = MembershipStatusFilter.Active)
    {
        var result = await _controller.ListMemberships(status);
        return ((OkObjectResult)result.Result!).Value.Should().BeAssignableTo<IReadOnlyList<MembershipDto>>().Subject;
    }

    private async Task SeedAsync(params (User Person, Membership Membership)[] rows)
    {
        foreach (var (person, membership) in rows)
        {
            membership.UserId = person.Id;
            _databaseContext.Users.Add(person);
            _databaseContext.Memberships.Add(membership);
        }

        await _databaseContext.SaveChangesAsync();
    }

    private static User Person(string email, string displayName) => new()
    {
        Id = Guid.NewGuid(),
        Email = email,
        DisplayName = displayName,
        CreatedAt = DateTime.UnixEpoch,
    };

    private static Membership Member(
        MembershipStatus status, Guid? organizationId = null, OrgRole role = OrgRole.Manager) => new()
        {
            OrganizationId = organizationId ?? CallingOrganizationId,
            Role = role,
            Status = status,
            JoinedAt = DateTime.UnixEpoch,
            DeactivatedAt = status == MembershipStatus.Deactivated ? DateTime.UnixEpoch : null,
        };
}
