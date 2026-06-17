using System.Security.Claims;
using SalesTrainer.Api.Features.Metrics.Services.Abstract;

namespace SalesTrainer.Api.Infrastructure.Metrics;

/// <summary>
/// Records authenticated activity for product metrics: increments the visit counter and
/// marks the user present in Redis. Runs after authentication so <c>context.User</c> is
/// populated. Infra paths (/metrics, /hangfire, ...) are skipped so the metrics endpoint
/// does not inflate its own counts.
/// </summary>
public sealed class ActivityTrackingMiddleware : IMiddleware
{
    private static readonly string[] IgnoredPrefixes =
        ["/metrics", "/hangfire", "/health", "/swagger"];

    private readonly IPresenceTracker _presenceTracker;
    private readonly ILogger<ActivityTrackingMiddleware> _logger;

    public ActivityTrackingMiddleware(
        IPresenceTracker presenceTracker,
        ILogger<ActivityTrackingMiddleware> logger)
    {
        _presenceTracker = presenceTracker;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        if (ShouldTrack(context))
        {
            AppMetrics.AuthenticatedRequests.Inc();

            var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? context.User.FindFirstValue("sub");

            if (!string.IsNullOrEmpty(userId))
            {
                try
                {
                    await _presenceTracker.MarkSeenAsync(userId, context.RequestAborted);
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    // Presence is best-effort; a Redis hiccup must never break the request.
                    _logger.LogWarning(exception, "Failed to mark user presence");
                }
            }
        }

        await next(context);
    }

    private static bool ShouldTrack(HttpContext context)
    {
        if (context.User?.Identity?.IsAuthenticated != true)
            return false;

        var path = context.Request.Path;
        foreach (var prefix in IgnoredPrefixes)
        {
            if (path.StartsWithSegments(prefix, StringComparison.OrdinalIgnoreCase))
                return false;
        }

        return true;
    }
}

public static class ActivityTrackingMiddlewareExtensions
{
    public static IApplicationBuilder UseActivityTracking(this IApplicationBuilder app) =>
        app.UseMiddleware<ActivityTrackingMiddleware>();
}
