using FluentAssertions;
using NUnit.Framework;

namespace Sellevate.RouteParity.Tests;

/// <summary>
/// Every externally reachable controller route must have a gateway route pointing at the service that
/// owns it.
///
/// <para>
/// <b>Why this exists.</b> <c>/assignments/*</c> and <c>/admin/assignments/*</c> lived through three
/// phases of work with no entry in <c>gateway/appsettings.json</c>, so the manager screen built in 40.23
/// returned 404 in every deployed environment while every test stayed green and three consecutive
/// reports described the feature as finished. Nothing in the suite could see it: the per-service tests
/// know nothing about the gateway, and the gateway's own tests assert hand-listed route names, so a
/// route nobody thought to list is a route nobody checks. <c>docs/DONT_FORGET.md</c> names this the most
/// expensive gap of Phase 40 and the first test to write once test-writing resumed.
/// </para>
///
/// <para>
/// The defect itself is long fixed — the sweep below currently finds zero gaps. The value is entirely
/// forward-looking: the next block that adds a controller and forgets the gateway fails here instead of
/// shipping a feature that never worked.
/// </para>
/// </summary>
[TestFixture]
public sealed class ControllerGatewayRouteParityTests
{
    private IReadOnlyList<GatewayRoute> _gatewayRoutes = null!;
    private IReadOnlyList<ControllerRoute> _controllerRoutes = null!;

    [OneTimeSetUp]
    public void LoadBothSides()
    {
        _gatewayRoutes = GatewayRoutingTable.Read();
        _controllerRoutes = ControllerRouteInventory.Collect();
    }

    /// <summary>
    /// Guards the check itself. Every assertion below is vacuously true against an empty inventory, and
    /// the two ways this file silently stops testing anything are a routing table that failed to copy
    /// and an assembly sweep that loaded no controllers.
    /// </summary>
    [Test]
    public void Both_sides_of_the_comparison_are_actually_populated()
    {
        _gatewayRoutes.Should().HaveCountGreaterThan(50);
        _controllerRoutes.Should().HaveCountGreaterThan(150);

        _controllerRoutes.Select(route => route.ClusterId).Distinct()
            .Should().BeEquivalentTo(ControllerRouteInventory.ClusterByAssembly.Values.Distinct(),
                "every service must contribute routes; a service contributing none means its assembly "
                + "did not load and its routes are going unchecked");
    }

    /// <summary>
    /// The check that would have caught the 40.23 incident: an endpoint no gateway route covers is
    /// unreachable from outside the cluster, and answers 404 with nothing failing locally.
    /// </summary>
    [Test]
    public void Every_public_controller_route_is_reachable_through_the_gateway()
    {
        var unreachable = _controllerRoutes
            .Where(route => !_gatewayRoutes.Any(gatewayRoute => gatewayRoute.Covers(route.LiteralTemplate)))
            .Select(route => $"{route.ClusterId}: {route.ControllerName}.{route.ActionName} -> /{route.LiteralTemplate}")
            .Distinct()
            .OrderBy(description => description, StringComparer.Ordinal)
            .ToList();

        unreachable.Should().BeEmpty(
            "each of these endpoints exists in a service and has no gateway route, so it answers 404 in "
            + "every deployed environment while every local test passes. Add a ReverseProxy route for "
            + "the prefix, or mark the controller with the service's InternalServiceAuthFilter if it is "
            + "meant to be reachable only service-to-service:\n"
            + string.Join('\n', unreachable));
    }

    /// <summary>
    /// A route that exists but points at the wrong cluster is worse than a missing one: the request
    /// reaches a service that has no such endpoint, so the answer is a 404 that looks like the gateway
    /// working correctly.
    ///
    /// <para>
    /// Resolution follows YARP's own rule — the most specific matching route wins — rather than assuming
    /// one prefix belongs to one service. That distinction is load-bearing here: <c>identity</c> owns
    /// <c>/profile/{**catch-all}</c> while <c>gamification</c> owns the more specific
    /// <c>/profile/achievements</c>, and a one-prefix-one-cluster assumption reports that deliberate
    /// overlap as a defect.
    /// </para>
    /// </summary>
    [Test]
    public void Every_route_is_served_by_the_cluster_that_owns_the_controller()
    {
        var mismatches = new List<string>();

        foreach (var route in _controllerRoutes)
        {
            var mostSpecific = _gatewayRoutes
                .Where(gatewayRoute => gatewayRoute.Covers(route.LiteralTemplate))
                .OrderByDescending(gatewayRoute => gatewayRoute.LiteralPrefix.Length)
                .FirstOrDefault();

            if (mostSpecific is null || mostSpecific.ClusterId == route.ClusterId)
            {
                continue;
            }

            mismatches.Add(
                $"/{route.LiteralTemplate} ({route.ControllerName}.{route.ActionName}) is owned by "
                + $"'{route.ClusterId}' but route '{mostSpecific.RouteId}' sends it to "
                + $"'{mostSpecific.ClusterId}'");
        }

        mismatches.Should().BeEmpty(
            "these requests reach a service that has no such endpoint, which answers 404 and looks like "
            + "correct gateway behaviour:\n" + string.Join('\n', mismatches.Distinct()));
    }

    /// <summary>
    /// A route pointing at an undeclared cluster fails at startup rather than at request time, but only
    /// once somebody starts the gateway. This makes it a build-time answer.
    /// </summary>
    [Test]
    public void Every_gateway_route_names_a_cluster_this_repository_actually_has()
    {
        var knownClusters = ControllerRouteInventory.ClusterByAssembly.Values.ToHashSet(StringComparer.Ordinal);

        _gatewayRoutes.Select(route => route.ClusterId).Distinct()
            .Should().BeSubsetOf(knownClusters);
    }

    /// <summary>
    /// Routes that forward a path no controller serves, and which are known and accepted.
    ///
    /// <para>
    /// <c>learning-admin-program-root</c> forwards the bare <c>/admin/program</c>, but
    /// <c>AdminProgramController</c> only ever serves <c>admin/program/versions</c> and
    /// <c>admin/program/enrollments</c>. It was found by this test on its first run. It is harmless —
    /// with the route, a request to exactly that path 404s at learning-service; without it, it 404s at
    /// the gateway — and it is deliberately <b>not</b> deleted: editing the routing table to tidy it is
    /// the exact class of change that cost three phases of work, and the payoff here is nil.
    /// </para>
    /// </summary>
    private static readonly string[] KnownOrphanedRoutes = ["learning-admin-program-root"];

    /// <summary>
    /// A route no controller can serve is dead configuration. It is not a defect on its own — a prefix
    /// may legitimately be reserved ahead of the controller that will serve it — so this pins the known
    /// set rather than demanding none: a <b>new</b> orphan fails here while somebody still remembers why
    /// the route was added, and removing a known one never fails.
    /// </summary>
    [Test]
    public void No_new_gateway_route_forwards_to_a_prefix_no_controller_serves()
    {
        var orphaned = _gatewayRoutes
            .Where(gatewayRoute => !_controllerRoutes.Any(route => gatewayRoute.Covers(route.LiteralTemplate)))
            .Where(gatewayRoute => !KnownOrphanedRoutes.Contains(gatewayRoute.RouteId, StringComparer.Ordinal))
            .Select(gatewayRoute => $"{gatewayRoute.RouteId} -> /{gatewayRoute.LiteralPrefix} ({gatewayRoute.ClusterId})")
            .OrderBy(description => description, StringComparer.Ordinal)
            .ToList();

        orphaned.Should().BeEmpty(
            "these gateway routes forward a prefix that no controller in the owning service serves, so "
            + "they are either dead configuration or a prefix whose controller was renamed. Add the "
            + "controller, remove the route, or list it in KnownOrphanedRoutes with the reason:\n"
            + string.Join('\n', orphaned));
    }
}
