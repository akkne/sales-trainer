using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sellevate.Learning.Common.Security;
using Sellevate.Learning.Features.SkillTree.Models;
using Sellevate.Learning.Infrastructure.Data;

namespace Sellevate.Learning.Features.SkillTree;

/// <summary>
/// C-3 audit fix. The one question ai-service asks learning-service to label a dialog bundle's
/// skill by id: what is its slug and title (docs/AUDIT_CONTRACTS.md, finding C-3).
///
/// <para>
/// <b>No tenant scope to resolve.</b> Skills are global content — <c>OrganizationId IS NULL</c> for
/// the whole of Phase 40.10 — so this reads <see cref="LearningDbContext.Skills"/> directly the same
/// way the superadmin content controllers under <c>Features/Admin</c> do
/// (<c>TenantTransactionScope</c>'s own remarks), rather than demanding an organization header a
/// platform-wide admin call would not carry.
/// </para>
/// </summary>
[ApiController]
[Route("internal/skills")]
[ServiceFilter(typeof(InternalServiceAuthFilter))]
public sealed class InternalSkillsController(LearningDbContext database) : ControllerBase
{
    [HttpGet("lookup")]
    public async Task<ActionResult<IReadOnlyList<SkillLookupDto>>> Lookup(CancellationToken cancellationToken)
    {
        var skills = await database.Skills
            .Select(skill => new SkillLookupDto(skill.Id, skill.IconicName, skill.Title))
            .ToListAsync(cancellationToken);

        return Ok(skills);
    }
}
