using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Sellevate.Identity.Features.Auth.Exceptions;

namespace Sellevate.Identity.Infrastructure;

/// <summary>
/// Turns the domain exceptions this service throws into ProblemDetails responses, and everything else
/// into a 500. Exception messages are echoed to the caller only below 500: a 4xx message is written for
/// a human to act on, while a 500 could carry internal detail and is logged instead.
/// </summary>
internal sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (statusCode, title) = exception switch
        {
            UnauthorizedAccessException => (StatusCodes.Status401Unauthorized, "Unauthorized"),
            EmailNotVerifiedException => (StatusCodes.Status403Forbidden, "Email not verified"),
            OrganizationSuspendedException => (StatusCodes.Status403Forbidden, "Organization suspended"),
            EmailVerificationCooldownException => (StatusCodes.Status429TooManyRequests, "Too many requests"),
            InvalidOperationException => (StatusCodes.Status400BadRequest, "Bad request"),
            KeyNotFoundException => (StatusCodes.Status404NotFound, "Not found"),
            _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred")
        };

        if (statusCode == StatusCodes.Status500InternalServerError)
        {
            logger.LogError(exception, "Unhandled exception");
        }
        else
        {
            logger.LogWarning(exception, "Handled domain exception -> {StatusCode}", statusCode);
        }

        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = statusCode < 500 ? exception.Message : null
        };

        httpContext.Response.StatusCode = statusCode;
        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);
        return true;
    }
}
