using System.Text.Json;
using FluentAssertions;
using NUnit.Framework;

namespace Sellevate.Analytics.Tests.Unit;

[TestFixture]
public class GatewayRouteFlipConfigurationTests
{
    private static JsonElement LoadGatewayReverseProxySection()
    {
        var gatewayAppSettingsPath = Path.GetFullPath(Path.Combine(
            TestContext.CurrentContext.TestDirectory,
            "..", "..", "..", "..", "..",
            "gateway", "Gateway", "appsettings.json"));

        File.Exists(gatewayAppSettingsPath).Should().BeTrue(
            $"the gateway appsettings.json should exist at {gatewayAppSettingsPath}");

        using var document = JsonDocument.Parse(File.ReadAllText(gatewayAppSettingsPath));
        return document.RootElement.GetProperty("ReverseProxy").Clone();
    }

    [Test]
    public void Gateway_RoutesTrackingPrefix_ToTheAnalyticsCluster()
    {
        var reverseProxy = LoadGatewayReverseProxySection();
        var routes = reverseProxy.GetProperty("Routes");

        var trackingRoute = routes.EnumerateObject()
            .Select(route => route.Value)
            .Single(route => route.GetProperty("Match").GetProperty("Path").GetString() == "/tracking/{**catch-all}");

        trackingRoute.GetProperty("ClusterId").GetString().Should().Be("analytics");
    }

    [Test]
    public void Gateway_DefinesAnalyticsCluster()
    {
        var reverseProxy = LoadGatewayReverseProxySection();
        var clusters = reverseProxy.GetProperty("Clusters");

        clusters.TryGetProperty("analytics", out _).Should().BeTrue();
    }

    [Test]
    public void Gateway_DoesNotRouteTrackingToTheMonolith()
    {
        var reverseProxy = LoadGatewayReverseProxySection();
        var routes = reverseProxy.GetProperty("Routes");

        var trackingRoutesPointingAtMonolith = routes.EnumerateObject()
            .Select(route => route.Value)
            .Where(route => route.GetProperty("Match").GetProperty("Path").GetString()?.StartsWith("/tracking") == true)
            .Where(route => route.GetProperty("ClusterId").GetString() == "monolith");

        trackingRoutesPointingAtMonolith.Should().BeEmpty();
    }
}
