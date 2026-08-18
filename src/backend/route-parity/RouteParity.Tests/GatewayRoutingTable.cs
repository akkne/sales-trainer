using System.Text.Json;

namespace Sellevate.RouteParity.Tests;

/// <summary>
/// The gateway's <c>ReverseProxy:Routes</c> section, read from the committed
/// <c>gateway/Gateway/appsettings.json</c> and reduced to what a parity check needs: the literal path
/// prefix of each route, whether it ends in a catch-all, and which cluster it forwards to.
/// </summary>
internal sealed record GatewayRoute(string RouteId, string LiteralPrefix, bool IsCatchAll, string ClusterId)
{
    /// <summary>
    /// Whether this route would serve <paramref name="template"/>.
    ///
    /// <para>
    /// A catch-all route covers its own prefix and everything beneath it; a bare route (the <c>-root</c>
    /// variants) covers only the collection URL itself, which is why those pairs exist — a catch-all
    /// does not match the bare path.
    /// </para>
    /// </summary>
    public bool Covers(string template) =>
        IsCatchAll
            ? template == LiteralPrefix || template.StartsWith(LiteralPrefix + "/", StringComparison.Ordinal)
            : template == LiteralPrefix;
}

internal static class GatewayRoutingTable
{
    private const string CatchAllSegment = "{**catch-all}";
    private const string LinkedConfigurationFileName = "gateway-appsettings.json";

    /// <summary>
    /// Reads the routing table. The file is linked into the test output by the project file, so this
    /// reads the same bytes the gateway is deployed with rather than a copy that can drift.
    /// </summary>
    public static IReadOnlyList<GatewayRoute> Read()
    {
        var configurationPath = Path.Combine(AppContext.BaseDirectory, LinkedConfigurationFileName);
        if (!File.Exists(configurationPath))
        {
            throw new FileNotFoundException(
                $"The gateway routing table was not copied to the test output. Expected "
                + $"'{configurationPath}'. The project file links "
                + "gateway/Gateway/appsettings.json as Content; if that link was removed, this check "
                + "silently stops testing anything.",
                configurationPath);
        }

        using var document = JsonDocument.Parse(File.ReadAllText(configurationPath));
        var routes = document.RootElement
            .GetProperty("ReverseProxy")
            .GetProperty("Routes");

        return routes.EnumerateObject()
            .Select(route => Parse(route.Name, route.Value))
            .ToList();
    }

    private static GatewayRoute Parse(string routeId, JsonElement route)
    {
        var clusterId = route.GetProperty("ClusterId").GetString()
            ?? throw new InvalidOperationException($"Route '{routeId}' has no ClusterId.");

        var path = route.GetProperty("Match").GetProperty("Path").GetString()
            ?? throw new InvalidOperationException($"Route '{routeId}' has no Match:Path.");

        var segments = path.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        var isCatchAll = segments.Length > 0 && segments[^1] == CatchAllSegment;
        var literalSegments = segments.Where(segment => !segment.StartsWith('{'));

        return new GatewayRoute(routeId, string.Join('/', literalSegments), isCatchAll, clusterId);
    }
}
