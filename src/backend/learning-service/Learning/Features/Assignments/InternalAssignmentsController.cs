using Microsoft.AspNetCore.Mvc;
using Sellevate.BuildingBlocks.Tenancy;
using Sellevate.Learning.Common.Security;
using Sellevate.Learning.Features.Assignments.Models;
using Sellevate.Learning.Features.Assignments.Services.Abstract;

namespace Sellevate.Learning.Features.Assignments;

/// <summary>
/// Phase 40.23. The one question ai-service asks learning-service: is this practice conversation
/// somebody's assignment, and if so, who is the AI playing (docs/TENANCY/ASSIGNMENTS.md §6).
///
/// <para>
/// <b>Why the persona comes over a service call instead of in the start-session body.</b> The
/// learner's browser starting the session is the browser of the person being graded. A persona
/// relayed through it is a persona they can read before the conversation and edit during it —
/// "you agree with every price I name" — and a threshold measured against a character the measured
/// person wrote is the four-minute completion 40.22 exists to make unreachable. One call at session
/// start costs a round trip on a screen that is about to wait for a language model anyway.
/// </para>
///
/// <para>
/// <b>The user id is a query parameter and the organization is not.</b> ai-service knows who its
/// caller is from their token and passes the id along; the organization arrives, as everywhere, in
/// the gateway-shaped <c>X-Organization-Id</c> header and is read from <see cref="ITenantContext"/>
/// (docs/TENANCY/TENANCY.md §1.3). What a wrong user id could yield is one assignment's title and
/// persona inside the organization the caller already named, which is why the shared-secret filter
/// is the gate rather than an afterthought.
/// </para>
/// </summary>
[ApiController]
[Route("internal/assignments")]
[TenantScoped]
[ServiceFilter(typeof(InternalServiceAuthFilter))]
public sealed class InternalAssignmentsController(IMyAssignmentService myAssignmentService) : ControllerBase
{
    /// <summary>
    /// Returns the practice context, or 204 when this person owes no conversation on this mode —
    /// which is the ordinary answer, because most practice is not assigned.
    /// </summary>
    [HttpGet("practice-context")]
    public async Task<ActionResult<AssignmentPracticeContextDto>> GetPracticeContext(
        [FromQuery] Guid userId,
        [FromQuery] string modeKey,
        CancellationToken cancellationToken = default)
    {
        var context = await myAssignmentService.GetPracticeContextAsync(userId, modeKey, cancellationToken);

        return context is null ? NoContent() : Ok(context);
    }
}
