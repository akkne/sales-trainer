using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sellevate.Organization.Common.Constants;
using Sellevate.Organization.Features.DemoRequests.Exceptions;
using Sellevate.Organization.Features.DemoRequests.Models;
using Sellevate.Organization.Features.DemoRequests.Services.Abstract;

namespace Sellevate.Organization.Features.DemoRequests.Endpoints;

/// <summary>
/// The public "Request a demo" lead-capture endpoint. Anonymous and not tenant-scoped for the same
/// reason <see cref="Models.DemoRequest"/> is not <c>ITenantScoped</c>: the caller has no account and
/// no organization yet — this is the route that produces the very first record of them.
/// </summary>
[ApiController]
[Route(RouteConstants.DemoRequestsBase)]
[AllowAnonymous]
public sealed class DemoRequestController(IDemoRequestService demoRequestService) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<DemoRequestAcceptedDto>> SubmitDemoRequest(
        [FromBody] CreateDemoRequestRequestDto request,
        CancellationToken cancellationToken)
    {
        try
        {
            var accepted = await demoRequestService.SubmitAsync(request, cancellationToken);
            return Accepted(accepted);
        }
        catch (DemoRequestCooldownException cooldownException)
        {
            Response.Headers.RetryAfter = cooldownException.RetryAfterSeconds.ToString();
            return StatusCode(
                StatusCodes.Status429TooManyRequests,
                new { message = cooldownException.Message, retryAfterSeconds = cooldownException.RetryAfterSeconds });
        }
    }
}
