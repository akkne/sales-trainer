using FluentAssertions;
using NUnit.Framework;
using Sellevate.Identity.Features.Auth.Models;
using Sellevate.Identity.Features.Membership.Models;

namespace Sellevate.Identity.Tests.Unit;

/// <summary>
/// Both enums are persisted by numeric value (<c>HasConversion&lt;int&gt;()</c> on
/// <c>User.Role</c>, <c>Membership.Role</c> and <c>Invite.Role</c>) and both are re-issued as
/// strings in the JWT. That makes every number and every name here a wire contract: reordering a
/// member silently re-labels rows already in the database, and renaming one silently breaks the
/// policies that match on the claim text. Pinning them is what makes the 2026-08-16 rename safe.
/// </summary>
[TestFixture]
public sealed class RoleEnumContractTests
{
    [Test]
    public void Platform_roles_keep_their_stored_numeric_values()
    {
        ((int)UserRole.User).Should().Be(0);
        ((int)UserRole.Admin).Should().Be(1,
            "1 meant exactly `global platform admin` before Phase 40.6 removed it, so reinstating "
            + "Admin at 1 lands any surviving legacy row back on the meaning it already had");
        ((int)UserRole.SuperAdmin).Should().Be(2);
    }

    [Test]
    public void Platform_roles_are_exactly_these_three()
    {
        Enum.GetNames<UserRole>().Should().BeEquivalentTo("User", "Admin", "SuperAdmin");
    }

    [Test]
    public void Organization_roles_keep_their_stored_numeric_values()
    {
        ((int)OrgRole.Manager).Should().Be(0);
        ((int)OrgRole.TenancyAdmin).Should().Be(1,
            "TenancyAdmin is the former OrgAdmin renamed in place — the column is an int, so the "
            + "rename needs no data migration and must not move the value");
        ((int)OrgRole.TenancySuperAdmin).Should().Be(2);
    }

    [Test]
    public void Organization_roles_are_exactly_these_three()
    {
        Enum.GetNames<OrgRole>().Should().BeEquivalentTo("Manager", "TenancyAdmin", "TenancySuperAdmin");
    }

    [Test]
    public void The_retired_OrgAdmin_name_no_longer_parses()
    {
        Enum.TryParse<OrgRole>("OrgAdmin", ignoreCase: true, out _).Should().BeFalse(
            "a stale caller must be refused rather than silently mapped onto TenancyAdmin");
    }
}
