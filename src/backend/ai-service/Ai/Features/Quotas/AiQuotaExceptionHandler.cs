using Microsoft.AspNetCore.Diagnostics;
using Sellevate.Ai.Features.Quotas.Constants;
using Sellevate.Ai.Features.Quotas.Models;

namespace Sellevate.Ai.Features.Quotas;

/// <summary>
/// Phase 40.33. Turns a quota refusal into the status code it means, everywhere at once.
///
/// <para>
/// Registered as an <see cref="IExceptionHandler"/> rather than a <c>catch</c> in each controller for
/// the reason <c>docs/LLM_FAILURE_HANDLING.md</c> gives about catching the base provider exception:
/// the failure mode of the per-controller version is the controller somebody adds next. Eleven call
/// sites reach the meter today and none of them knows it exists.
/// </para>
///
/// <para>
/// <b>429, not 402 or 403.</b> A spent allowance is «приходите позже или купите больше», which is
/// what 429 already means to every client in this codebase — the voice gate has answered 429 since
/// the feature shipped, and the frontend already renders it. 402 is reserved for the *provider*
/// telling us our own balance is empty (<c>OpenAiPaymentRequiredException</c>), and conflating the
/// two would make our customer's cap look like our own outage.
/// </para>
///
/// <para>
/// An unattributed call is 400 and not 500: it is a caller mistake with a fixed remedy — forward
/// <c>X-Organization-Id</c> — and reporting it as a server fault would send somebody looking through
/// ai-service logs for a bug that lives in the client.
/// </para>
/// </summary>
internal sealed class AiQuotaExceptionHandler(ILogger<AiQuotaExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        switch (exception)
        {
            case AiQuotaExceededException quotaExceeded:
                httpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                await httpContext.Response.WriteAsJsonAsync(
                    new
                    {
                        error = AiQuotaFailureMessages.QuotaReached,
                        resource = quotaExceeded.Resource,
                        period = quotaExceeded.Period,
                        used = quotaExceeded.Used,
                        limit = quotaExceeded.Limit,
                    },
                    cancellationToken);
                return true;

            case AiUnattributedCallException unattributed:
                logger.LogWarning(unattributed, "Rejected an unattributed metered call to {Path}", httpContext.Request.Path);
                httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
                await httpContext.Response.WriteAsJsonAsync(
                    new { error = AiQuotaFailureMessages.OrganizationRequired },
                    cancellationToken);
                return true;

            default:
                return false;
        }
    }
}
