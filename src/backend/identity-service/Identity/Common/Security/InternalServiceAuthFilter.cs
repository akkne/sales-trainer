using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Sellevate.Identity.Common.Security;

/// <summary>
/// Phase 40.23. Rejects requests to identity-service's internal service-to-service endpoints that
/// do not carry the shared secret configured as <c>InternalAuth:ServiceSecret</c>. A copy of
/// ai-service's filter of the same name, kept local rather than lifted into BuildingBlocks for the
/// same reason integration-event contracts are copied per service: the two are agreeing on a wire
/// header, not sharing a type, and one of them changing must not silently change the other.
///
/// <para>
/// <b>What this guards is a membership list, so the posture matters.</b> The endpoints behind it
/// carry no JWT — a calling service has no user — and take their organization from the
/// gateway-style <c>X-Organization-Id</c> header. Reachable directly, that combination is a
/// cross-tenant read for anybody who can address the pod, and the secret is the only thing standing
/// in front of it. Two consequences follow, both deliberate: the endpoints return **user ids and
/// nothing else** — no names, no emails, no roles — so a breach of the secret leaks opaque
/// identifiers rather than an employee directory; and the filter's dev behaviour (open when the
/// secret is unset) is inherited from ai-service rather than reinvented, so there is one rule to
/// remember about internal routes instead of two.
/// </para>
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
        // Left open when no secret is configured (dev / single-service mode), matching
        // ai-service's filter. docs/DONT_FORGET.md carries the deployment note.
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
