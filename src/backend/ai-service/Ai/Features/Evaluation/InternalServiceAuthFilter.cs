using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Sellevate.Ai.Features.Evaluation;

/// <summary>
/// AI7b: Rejects requests to internal service endpoints that do not supply the
/// correct shared-secret header configured via <c>InternalAuth:ServiceSecret</c>.
/// Intended to guard <see cref="EvaluationController"/> from unauthenticated
/// callers when the pod is reachable directly (no JWT required for service-to-service calls).
/// </summary>
public sealed class InternalServiceAuthFilter : IActionFilter
{
    private const string HeaderName = "X-Internal-Service-Secret";

    private readonly string? _expectedSecret;
    private readonly ILogger<InternalServiceAuthFilter> _logger;

    public InternalServiceAuthFilter(IConfiguration configuration, ILogger<InternalServiceAuthFilter> logger)
    {
        _expectedSecret = configuration["InternalAuth:ServiceSecret"];
        _logger = logger;
    }

    public void OnActionExecuting(ActionExecutingContext context)
    {
        // If no secret is configured the endpoint is left open (dev / single-service mode).
        if (string.IsNullOrWhiteSpace(_expectedSecret))
            return;

        if (!context.HttpContext.Request.Headers.TryGetValue(HeaderName, out var provided) ||
            !string.Equals(provided, _expectedSecret, StringComparison.Ordinal))
        {
            _logger.LogWarning(
                "Rejected unauthenticated internal request to {Path} from {RemoteIp}",
                context.HttpContext.Request.Path,
                context.HttpContext.Connection.RemoteIpAddress);

            context.Result = new ObjectResult(new { message = "Forbidden" })
            {
                StatusCode = StatusCodes.Status403Forbidden
            };
        }
    }

    public void OnActionExecuted(ActionExecutedContext context) { }
}
