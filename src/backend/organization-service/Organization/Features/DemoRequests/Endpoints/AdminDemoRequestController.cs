using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sellevate.Organization.Common.Constants;
using Sellevate.Organization.Features.DemoRequests.Models;
using Sellevate.Organization.Features.DemoRequests.Services.Abstract;

namespace Sellevate.Organization.Features.DemoRequests.Endpoints;

/// <summary>
/// The platform-staff surface over submitted demo requests: the list a sales rep works from, and the
/// status update that records what happened after they reached out.
/// </summary>
[ApiController]
[Route(RouteConstants.AdminDemoRequestsBase)]
[Authorize(Policy = AuthorizationPolicies.RequirePlatformAdministrator)]
public sealed class AdminDemoRequestController(IDemoRequestService demoRequestService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<DemoRequestDto>>> ListDemoRequests(
        CancellationToken cancellationToken)
    {
        var demoRequests = await demoRequestService.ListDemoRequestsAsync(cancellationToken);
        return Ok(demoRequests);
    }

    [HttpPatch(RouteConstants.AdminDemoRequestStatus)]
    public async Task<ActionResult<DemoRequestDto>> UpdateDemoRequestStatus(
        Guid id,
        [FromBody] UpdateDemoRequestStatusRequestDto request,
        CancellationToken cancellationToken)
    {
        var demoRequest = await demoRequestService.UpdateStatusAsync(id, request.Status, cancellationToken);
        return demoRequest is null ? NotFound() : Ok(demoRequest);
    }
}
