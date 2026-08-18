using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Sellevate.Learning.Common.Security;

/// <summary>
/// Phase 40.23. Rejects requests to learning-service's internal service-to-service endpoints that do
/// not carry the shared secret configured as <c>InternalAuth:ServiceSecret</c>. A third copy of the
/// filter ai-service and identity-service already have, kept local for the same reason the
/// integration-event contracts are copied per service: services agree on a wire header, not on a
/// type, and one of them changing must not silently change the others.
///
/// <para>
/// <b>40.34 made the missing secret fail closed outside Development.</b> The three copies used to
/// allow the request when no secret was configured, and the key was configured in no compose file
/// and no <c>.env.example</c> — so in every shipped configuration all three filters were no-ops in
/// front of routes that carry no JWT and take their tenant from a plain header. A config omission
/// silently disabling an authentication check is how that shipped; it now refuses instead.
/// </para>
/// </summary>
public sealed class InternalServiceAuthFilter : IActionFilter
{
    private const string HeaderName = "X-Internal-Service-Secret";

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
            context.Result = new ObjectResult(new { message = "Forbidden" })
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

            context.Result = new ObjectResult(new { message = "Forbidden" })
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
