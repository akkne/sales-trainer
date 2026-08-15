using System.Reflection;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using NUnit.Framework;
using Sellevate.Organization.Common.Constants;
using Sellevate.Organization.Features.Organizations.Endpoints;

namespace Sellevate.Organization.Tests.Unit;

/// <summary>
/// Phase 40.9 closed the placeholder that let any authenticated user create, rename or suspend an
/// organization. The registry is the one place where addressing an organization by a route id is
/// legitimate (docs/TENANCY/TENANCY.md §1.3), and that licence rests entirely on this gate — so
/// the gate is asserted rather than assumed.
/// </summary>
[TestFixture]
public sealed class OrganizationControllerAuthorizationTests
{
    [Test]
    public void Organization_registry_routes_require_a_platform_super_admin()
    {
        var authorizeAttribute = typeof(OrganizationController).GetCustomAttribute<AuthorizeAttribute>();

        authorizeAttribute.Should().NotBeNull();
        authorizeAttribute!.Policy.Should().Be(AuthorizationPolicies.RequireSuperAdmin);
    }

    [Test]
    public void Organization_profile_routes_are_not_platform_gated()
    {
        var authorizeAttribute = typeof(OrganizationProfileController).GetCustomAttribute<AuthorizeAttribute>();

        authorizeAttribute.Should().NotBeNull();
        authorizeAttribute!.Policy.Should().BeNull(
            "the profile belongs to the organization itself and is read by its own members");
    }
}
