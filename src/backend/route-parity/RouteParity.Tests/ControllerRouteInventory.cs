using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;

namespace Sellevate.RouteParity.Tests;

/// <summary>One action's externally reachable path, and which service owns it.</summary>
internal sealed record ControllerRoute(
    string ClusterId,
    string ControllerName,
    string ActionName,
    string LiteralTemplate);

/// <summary>
/// Every HTTP route the nine services declare, assembled by reflection over the built assemblies.
///
/// <para>
/// <b>Reflection, not a source scan.</b> Nine controllers declare their route through a constant —
/// <c>[Route(RouteConstants.OrganizationProfileBase)]</c> and friends. A regex over source sees the
/// member name and yields nothing, which during the design of this check produced ten false "missing
/// route" reports and one false cluster mismatch. Constant values are baked into the attribute blob and
/// come back correctly from <c>GetCustomAttribute</c>.
/// </para>
/// </summary>
internal static class ControllerRouteInventory
{
    /// <summary>
    /// Assembly name to gateway cluster id. The gateway's cluster names and the assembly names are the
    /// two halves this whole check compares, so the mapping is stated once, here.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> ClusterByAssembly =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Sellevate.Ai"] = "ai",
            ["Sellevate.Analytics"] = "analytics",
            ["Sellevate.Company"] = "company",
            ["Sellevate.Gamification"] = "gamification",
            ["Sellevate.Identity"] = "identity",
            ["Sellevate.Learning"] = "learning",
            ["Sellevate.Notification"] = "notification",
            ["Sellevate.Organization"] = "organization",
            ["Sellevate.Social"] = "social",
        };

    /// <summary>
    /// Path prefixes that are deliberately absent from the routing table.
    ///
    /// <para>
    /// <c>internal/</c> is the naming convention for service-to-service routes, and <c>healthz</c> and
    /// <c>metrics</c> are scraped inside the cluster. <b>The convention alone is not enough:</b>
    /// ai-service serves several service-to-service endpoints under the plain <c>ai/</c> prefix, which
    /// reads like a public one. Those are excluded by the filter they carry rather than by their name —
    /// see <see cref="IsInternalOnly"/> — which is the stronger rule, because it is tied to the thing
    /// that actually makes a route unreachable from outside.
    /// </para>
    /// </summary>
    private static readonly string[] UnroutedPrefixes = ["internal", "healthz", "metrics", "swagger"];

    /// <summary>
    /// The attribute name that marks a controller as callable only by another service holding the shared
    /// secret. Matched by name because each service declares its own copy of the filter.
    /// </summary>
    private const string InternalServiceAuthFilterName = "InternalServiceAuthFilter";

    public static IReadOnlyList<ControllerRoute> Collect()
    {
        var routes = new List<ControllerRoute>();

        foreach (var (assemblyName, clusterId) in ClusterByAssembly)
        {
            var assembly = Assembly.Load(new AssemblyName(assemblyName));

            foreach (var controllerType in assembly.GetTypes().Where(IsController))
            {
                if (IsInternalOnly(controllerType))
                {
                    continue;
                }

                var classTemplate = controllerType.GetCustomAttribute<RouteAttribute>()?.Template;

                foreach (var action in controllerType.GetMethods(BindingFlags.Public | BindingFlags.Instance))
                {
                    foreach (var httpMethod in action.GetCustomAttributes<HttpMethodAttribute>())
                    {
                        var template = Compose(classTemplate, httpMethod.Template);
                        if (template is null || IsUnrouted(template))
                        {
                            continue;
                        }

                        routes.Add(new ControllerRoute(
                            clusterId, controllerType.Name, action.Name, template));
                    }
                }
            }
        }

        return routes;
    }

    private static bool IsController(Type type) =>
        type is { IsAbstract: false, IsPublic: true }
        && typeof(ControllerBase).IsAssignableFrom(type);

    private static bool IsInternalOnly(Type controllerType) =>
        controllerType.GetCustomAttributes<ServiceFilterAttribute>()
            .Any(filter => filter.ServiceType.Name == InternalServiceAuthFilterName);

    /// <summary>
    /// Combines the class and method templates the way ASP.NET Core does, and reduces the result to its
    /// literal segments.
    ///
    /// <para>
    /// Two mechanics that a naive concatenation gets wrong, both present in this codebase. A method
    /// template beginning with <c>/</c> is absolute and <b>discards</b> the class-level template —
    /// <c>[Route("ai/chat")]</c> with <c>[HttpPost("/ai/tts")]</c> serves <c>/ai/tts</c>, not
    /// <c>/ai/chat/ai/tts</c>. And twenty-six controllers carry no class-level <c>[Route]</c> at all,
    /// putting the whole path on the method, so the class template must be treated as absent rather
    /// than as an empty prefix.
    /// </para>
    /// </summary>
    private static string? Compose(string? classTemplate, string? methodTemplate)
    {
        var combined = methodTemplate switch
        {
            null or "" => classTemplate,
            _ when methodTemplate.StartsWith('/') => methodTemplate,
            _ when string.IsNullOrEmpty(classTemplate) => methodTemplate,
            _ => $"{classTemplate.TrimEnd('/')}/{methodTemplate}",
        };

        if (string.IsNullOrWhiteSpace(combined))
        {
            return null;
        }

        var literalSegments = combined.Trim('/')
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .TakeWhile(segment => !segment.StartsWith('{'));

        var template = string.Join('/', literalSegments);
        return string.IsNullOrEmpty(template) ? null : template;
    }

    private static bool IsUnrouted(string template) =>
        UnroutedPrefixes.Any(prefix =>
            template == prefix || template.StartsWith(prefix + "/", StringComparison.Ordinal));
}
