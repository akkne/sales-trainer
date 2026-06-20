using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sellevate.Learning.Common.Extensions;
using Sellevate.Learning.Features.Techniques.Models;
using Sellevate.Learning.Features.Techniques.Services.Abstract;

namespace Sellevate.Learning.Features.Techniques;

[ApiController]
[Authorize]
public sealed class TechniqueController(ITechniqueService techniqueService) : ControllerBase
{
    [HttpGet("techniques")]
    public async Task<ActionResult<IReadOnlyList<TechniqueCardDto>>> GetTechniques(
        [FromQuery] string? skill,
        [FromQuery] string? search,
        [FromQuery(Name = "tag")] string[]? tag,
        CancellationToken cancellationToken)
    {
        var currentUserId = TryGetCurrentUserId();
        var cards = await techniqueService.GetTechniqueCardsAsync(
            currentUserId, skill, search, tag, cancellationToken);
        return Ok(cards);
    }

    [HttpGet("techniques/meta")]
    public async Task<ActionResult<TechniqueMetaDto>> GetTechniqueMeta(
        CancellationToken cancellationToken)
    {
        var currentUserId = TryGetCurrentUserId();
        var meta = await techniqueService.GetTechniqueMetaAsync(currentUserId, cancellationToken);
        return Ok(meta);
    }

    [HttpGet("techniques/{slug}")]
    public async Task<ActionResult<TechniqueDetailDto>> GetTechnique(
        string slug,
        CancellationToken cancellationToken)
    {
        var currentUserId = TryGetCurrentUserId();
        var detail = await techniqueService.GetTechniqueBySlugAsync(slug, currentUserId, cancellationToken);
        if (detail is null) return NotFound();
        return Ok(detail);
    }

    [HttpPost("techniques/{slug}/seen")]
    public async Task<IActionResult> MarkTechniqueSeen(
        string slug,
        CancellationToken cancellationToken)
    {
        var currentUserId = TryGetCurrentUserId();
        if (!currentUserId.HasValue) return Unauthorized();

        await techniqueService.MarkTechniqueSeenAsync(slug, currentUserId.Value, cancellationToken);
        return NoContent();
    }

    private Guid? TryGetCurrentUserId()
    {
        return User.TryResolveUserId(out var currentUserId) ? currentUserId : null;
    }
}
