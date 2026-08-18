using System.Security.Cryptography;
using System.Text;
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
/// identifiers rather than an employee directory; and the filter refuses rather than
/// allows when the secret is unset outside Development (40.34 — before that it allowed, and the key
/// was configured nowhere, so this check was a no-op in every shipped configuration).
/// </para>
/// </summary>
public sealed class InternalServiceAuthFilter : IActionFilter
{
    private const string HeaderName = "X-Internal-Service-Secret";
    private const string ForbiddenMessage = "Forbidden";

    private readonly string? _expectedSecret;
    private readonly bool _isDevelopment;
    private readonly ILogger<InternalServiceAuthFilter> _logger;

    public InternalServiceAuthFilter(
        IConfiguration configuration, IHostEnvironment environment, ILogger<InternalServiceAuthFilter> logger)
    {
        _expectedSecret = configuration["InternalAuth:ServiceSecret"];
        _isDevelopment = environment.IsDevelopment();
        _logger = logger;
    }

    public void OnActionExecuting(ActionExecutingContext context)
    {
        if (string.IsNullOrWhiteSpace(_expectedSecret))
        {
            if (_isDevelopment)
            {
                return;
            }

            _logger.LogError(
                "InternalAuth:ServiceSecret is not configured; refusing internal request to {Path}", context.HttpContext.Request.Path);
            context.Result = new ObjectResult(new { message = ForbiddenMessage })
            {
                StatusCode = StatusCodes.Status403Forbidden,
            };
            return;
        }

        if (!context.HttpContext.Request.Headers.TryGetValue(HeaderName, out var provided)
            || !IsExpectedSecret(provided.ToString()))
        {
            _logger.LogWarning(
                "Rejected unauthenticated internal request to {Path} from {RemoteIp}",
                context.HttpContext.Request.Path,
                context.HttpContext.Connection.RemoteIpAddress);

            context.Result = new ObjectResult(new { message = ForbiddenMessage })
            {
                StatusCode = StatusCodes.Status403Forbidden,
            };
        }
    }

    public void OnActionExecuted(ActionExecutedContext context)
    {
    }

    private bool IsExpectedSecret(string provided)
        => CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(provided), Encoding.UTF8.GetBytes(_expectedSecret!));
}
