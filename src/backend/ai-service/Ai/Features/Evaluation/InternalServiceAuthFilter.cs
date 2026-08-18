using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Sellevate.Ai.Features.Evaluation;

/// <summary>
/// AI7b: Rejects requests to internal service endpoints that do not supply the
/// correct shared-secret header configured via <c>InternalAuth:ServiceSecret</c>.
/// Intended to guard <see cref="EvaluationController"/> from unauthenticated
/// callers when the pod is reachable directly (no JWT required for service-to-service calls).
///
/// <para>
/// 40.34: an unset secret refuses outside Development instead of allowing. The key was configured
/// in no compose file, so this check was a no-op wherever it actually mattered.
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
                return;

            _logger.LogError(
                "InternalAuth:ServiceSecret is not configured; refusing internal request to {Path}", context.HttpContext.Request.Path);
            context.Result = new ObjectResult(new { message = "Forbidden" })
            {
                StatusCode = StatusCodes.Status403Forbidden
            };
            return;
        }

        if (!context.HttpContext.Request.Headers.TryGetValue(HeaderName, out var provided) ||
            !IsExpectedSecret(provided.ToString()))
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

    private bool IsExpectedSecret(string provided)
        => CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(provided), Encoding.UTF8.GetBytes(_expectedSecret!));
}
