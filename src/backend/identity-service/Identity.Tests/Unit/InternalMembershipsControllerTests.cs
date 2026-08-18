using System.Reflection;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using NUnit.Framework;
using Sellevate.BuildingBlocks.Tenancy;
using Sellevate.Identity.Common.Security;
using Sellevate.Identity.Features.Membership.Endpoints;
using Sellevate.Identity.Features.Membership.Models;
using Sellevate.Identity.Infrastructure.Data;
using Sellevate.Identity.Tests.Helpers;

namespace Sellevate.Identity.Tests.Unit;

/// <summary>
/// <c>GET /internal/memberships/active</c> — the one route in this service that hands an
/// organization's roster to another service, and the only one in the Phase 40 backlog that publishes
/// the composition of an organization outward. It had no test at all from its introduction in 40.23
/// through the 40.26 extension that added the administrator list.
///
/// <para>
/// Three properties are load-bearing and each fails silently rather than loudly, which is why they are
/// pinned here: a deactivated member reappearing means an email to an ex-employee about homework and a
/// permanent "has not started" row on the sales lead's screen; a Manager appearing among the
/// administrators means the "your team has not started" notice going to the team instead of to the
/// person meant to apply pressure; and the route losing either of its two attributes means anybody able
/// to address the pod can read any organization's roster.
/// </para>
/// </summary>
[TestFixture]
public sealed class InternalMembershipsControllerTests
{
    private static readonly Guid CallingOrganizationId = Guid.Parse("aaaaaaaa-0000-4000-8000-000000000001");
    private static readonly Guid OtherOrganizationId = Guid.Parse("bbbbbbbb-0000-4000-8000-000000000002");

    private IdentityDbContext _databaseContext = null!;
    private TenantContext _tenantContext = null!;
    private InternalMembershipsController _controller = null!;

    [SetUp]
    public void SetUp()
    {
        _tenantContext = new TenantContext();
        _tenantContext.SetOrganization(CallingOrganizationId);
        _databaseContext = InMemoryDbContextFactory.Create(
            databaseName: $"internal-memberships-{Guid.NewGuid()}", _tenantContext);
        _controller = new InternalMembershipsController(_databaseContext, _tenantContext);
    }

    [TearDown]
    public void TearDown() => _databaseContext.Dispose();

    private async Task SeedAsync(params Membership[] memberships)
    {
        _databaseContext.Memberships.AddRange(memberships);
        await _databaseContext.SaveChangesAsync();
    }

    private static Membership Member(
        Guid userId,
        OrgRole role = OrgRole.Manager,
        MembershipStatus status = MembershipStatus.Active,
        Guid? organizationId = null) =>
        new()
        {
            UserId = userId,
            OrganizationId = organizationId ?? CallingOrganizationId,
            Role = role,
            Status = status,
            JoinedAt = DateTime.UnixEpoch,
            DeactivatedAt = status == MembershipStatus.Deactivated ? DateTime.UnixEpoch : null,
        };

    private static OrganizationMemberIdsDto Payload(ActionResult<OrganizationMemberIdsDto> result) =>
        (OrganizationMemberIdsDto)((OkObjectResult)result.Result!).Value!;

    private static Guid User(int index) =>
        Guid.Parse($"cccccccc-0000-4000-8000-{index:D12}");

    /// <summary>
    /// The administrator list is a subset of the user list, and it holds only the two tenancy
    /// administrator roles. A Manager reaching it would send "the team has not started" to the team.
    /// </summary>
    [Test]
    public async Task Administrators_are_a_subset_of_the_roster_and_hold_only_the_two_tenancy_roles()
    {
        await SeedAsync(
            Member(User(1)),
            Member(User(2)),
            Member(User(3), OrgRole.TenancyAdmin),
            Member(User(4), OrgRole.TenancySuperAdmin));

        var payload = Payload(await _controller.GetActiveMemberIds());

        payload.UserIds.Should().BeEquivalentTo([User(1), User(2), User(3), User(4)]);
        payload.AdministratorUserIds.Should().BeEquivalentTo([User(3), User(4)]);
        payload.AdministratorUserIds.Should().BeSubsetOf(payload.UserIds);
    }

    [Test]
    public async Task A_manager_is_never_an_administrator()
    {
        await SeedAsync(Member(User(1)), Member(User(2)));

        var payload = Payload(await _controller.GetActiveMemberIds());

        payload.UserIds.Should().HaveCount(2);
        payload.AdministratorUserIds.Should().BeEmpty();
    }

    /// <summary>
    /// Leaving an organization is a status change, not a row deletion (Phase 40.7), so this filter is
    /// the only thing standing between an ex-employee and every assignment issued after their last day.
    /// </summary>
    [Test]
    public async Task A_deactivated_member_is_in_neither_list()
    {
        await SeedAsync(
            Member(User(1)),
            Member(User(2), status: MembershipStatus.Deactivated));

        var payload = Payload(await _controller.GetActiveMemberIds());

        payload.UserIds.Should().Equal(User(1));
        payload.AdministratorUserIds.Should().BeEmpty();
    }

    /// <summary>
    /// A deactivated administrator is the more expensive half of the same rule: an email to a former
    /// manager listing the names of their former subordinates is about data, not convenience.
    /// </summary>
    [Test]
    public async Task A_deactivated_administrator_is_in_neither_list()
    {
        await SeedAsync(
            Member(User(1), OrgRole.TenancyAdmin),
            Member(User(2), OrgRole.TenancySuperAdmin, MembershipStatus.Deactivated));

        var payload = Payload(await _controller.GetActiveMemberIds());

        payload.UserIds.Should().Equal(User(1));
        payload.AdministratorUserIds.Should().Equal(User(1));
    }

    /// <summary>
    /// The organization is read from <see cref="ITenantContext"/> and from nothing in the request, so
    /// another organization's roster is not reachable through this route at all.
    /// </summary>
    [Test]
    public async Task Another_organizations_members_are_never_returned()
    {
        await SeedAsync(
            Member(User(1)),
            Member(User(2), OrgRole.TenancyAdmin, organizationId: OtherOrganizationId),
            Member(User(3), organizationId: OtherOrganizationId));

        var payload = Payload(await _controller.GetActiveMemberIds());

        payload.UserIds.Should().Equal(User(1));
        payload.AdministratorUserIds.Should().BeEmpty();
    }

    [Test]
    public async Task An_organization_with_no_active_members_returns_two_empty_lists_rather_than_failing()
    {
        await SeedAsync(Member(User(1), status: MembershipStatus.Deactivated));

        var payload = Payload(await _controller.GetActiveMemberIds());

        payload.UserIds.Should().BeEmpty();
        payload.AdministratorUserIds.Should().BeEmpty();
    }

    /// <summary>
    /// "No organization" must never widen into "everybody". Unreachable in production because
    /// <see cref="TenantScopedAttribute"/> refuses the request first, and kept for exactly that reason.
    /// </summary>
    [Test]
    public async Task A_request_with_no_organization_is_refused_rather_than_answered()
    {
        await SeedAsync(Member(User(1)), Member(User(2), OrgRole.TenancyAdmin));

        var tenantContextWithoutOrganization = new TenantContext();
        using var databaseContext = InMemoryDbContextFactory.Create(
            databaseName: $"internal-memberships-none-{Guid.NewGuid()}", tenantContextWithoutOrganization);
        var controller = new InternalMembershipsController(databaseContext, tenantContextWithoutOrganization);

        var result = await controller.GetActiveMemberIds();

        result.Result.Should().BeOfType<ForbidResult>();
    }

    /// <summary>
    /// Both attributes are the route's entire protection, and losing either is invisible in a diff.
    /// Without <see cref="InternalServiceAuthFilter"/> anybody able to address the pod could name an
    /// organization and read its roster; without <see cref="TenantScopedAttribute"/> a request carrying
    /// no <c>X-Organization-Id</c> would reach the action instead of being refused by the middleware.
    /// </summary>
    [Test]
    public void The_route_is_guarded_by_the_internal_service_secret_and_by_tenant_scoping()
    {
        var controllerType = typeof(InternalMembershipsController);

        controllerType.GetCustomAttribute<TenantScopedAttribute>().Should().NotBeNull();

        controllerType.GetCustomAttributes<ServiceFilterAttribute>()
            .Select(attribute => attribute.ServiceType)
            .Should().Contain(typeof(InternalServiceAuthFilter));
    }

    /// <summary>
    /// The route lives under <c>internal/</c> on purpose: that prefix is what keeps it out of the
    /// gateway's routing table, so it is unreachable from outside the cluster.
    /// </summary>
    [Test]
    public void The_route_stays_under_the_internal_prefix()
    {
        typeof(InternalMembershipsController).GetCustomAttribute<RouteAttribute>()!.Template
            .Should().Be("internal/memberships");

        typeof(InternalMembershipsController).GetMethod(nameof(InternalMembershipsController.GetActiveMemberIds))!
            .GetCustomAttribute<HttpGetAttribute>()!.Template.Should().Be("active");
    }

    /// <summary>
    /// The payload carries ids and nothing else. Returning a role per member would publish an
    /// organization's role directory to every service holding the shared secret, which is why the
    /// administrator filter is applied here rather than by the caller.
    /// </summary>
    [Test]
    public void The_payload_publishes_ids_only_and_never_a_role_directory()
    {
        typeof(OrganizationMemberIdsDto).GetProperties().Select(property => property.Name)
            .Should().BeEquivalentTo([nameof(OrganizationMemberIdsDto.UserIds),
                nameof(OrganizationMemberIdsDto.AdministratorUserIds)]);
    }
}
