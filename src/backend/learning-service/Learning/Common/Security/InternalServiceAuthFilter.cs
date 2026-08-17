using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Sellevate.Learning.Common.Security;

/// <summary>
/// Phase 40.23. Rejects requests to learning-service's internal service-to-service endpoints that do
/// not carry the shared secret configured as <c>InternalAuth:ServiceSecret</c>. A third copy of the
/// filter ai-service and identity-service already have, kept local for the same reason the
/// integration-event contracts are copied per service: services agree on a wire header, not on a
/// type, and one of them changing must not silently change the others.
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
        // Left open when no secret is configured (dev / single-service mode), matching the
        // ai-service filter this is a copy of. docs/DONT_FORGET.md carries the deployment note.
        if (string.IsNullOrWhiteSpace(_expectedSecret))
        {
            return;
        }

        if (!context.HttpContext.Request.Headers.TryGetValue(HeaderName, out var provided)
            || !string.Equals(provided, _expectedSecret, StringComparison.Ordinal))
        {
            _logger.LogWarning(
                "Rejected unauthenticated internal request to {Path} from {RemoteIp}",
                context.HttpContext.Request.Path,
                context.HttpContext.Connection.RemoteIpAddress);

            context.Result = new ObjectResult(new { message = "Forbidden" })
            {
                StatusCode = StatusCodes.Status403Forbidden,
            };
        }
    }

    public void OnActionExecuted(ActionExecutedContext context)
    {
    }
}
