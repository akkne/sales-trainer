using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sellevate.Learning.Common.Extensions;
using Sellevate.Learning.Features.Assignments.Models;
using Sellevate.Learning.Features.Assignments.Services.Abstract;

namespace Sellevate.Learning.Features.Assignments;

/// <summary>
/// Phase 40.23. The manager's own assignments — the roadmap's "активное задание первым экраном у
/// менеджера, пока не выполнено" (docs/TENANCY/ASSIGNMENTS.md §1).
///
/// <para>
/// <b>The route takes no user id and never will</b>, the same shape <c>ProgramController</c> uses:
/// the caller is whoever the token says, so "read somebody else's assignments" is not a request this
/// surface can express. There is no admin variant here either — the РОП reads the same rows through
/// <c>/admin/assignments/:id/progress</c>, which is gated separately.
/// </para>
///
/// <para>
/// <b>An empty list is a normal answer, and the client must treat it as one.</b> A manager with
/// nothing assigned has to see their skill tree, not a screen explaining that they have no
/// assignments — the card is an addition to the home screen, never a replacement for it.
/// </para>
/// </summary>
[ApiController]
[Authorize]
public sealed class AssignmentsController(IMyAssignmentService myAssignmentService) : ControllerBase
{
    [HttpGet("assignments/active")]
    public async Task<ActionResult<IReadOnlyList<ActiveAssignmentDto>>> GetActive(
        CancellationToken cancellationToken = default)
    {
        if (!User.TryResolveUserId(out var userId)) return Unauthorized();

        return Ok(await myAssignmentService.GetActiveForUserAsync(userId, cancellationToken));
    }
}
