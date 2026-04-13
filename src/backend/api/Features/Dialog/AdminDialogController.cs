using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SalesTrainer.Api.Features.Dialog.Models;
using SalesTrainer.Api.Infrastructure.Data;

namespace SalesTrainer.Api.Features.Dialog;

[ApiController]
[Route("admin/dialog")]
[Authorize(Policy = "RequireAdmin")]
public class AdminDialogController : ControllerBase
{
    private readonly AppDbContext _dbContext;
    private readonly ILogger<AdminDialogController> _logger;

    public AdminDialogController(AppDbContext dbContext, ILogger<AdminDialogController> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    [HttpGet("bundles")]
    public async Task<IActionResult> GetAllBundles()
    {
        var bundles = await _dbContext.DialogBundles
            .Include(bundle => bundle.Skill)
            .OrderBy(bundle => bundle.SortOrder)
            .ToListAsync();

        var bundleDtos = bundles.Select(DialogBundleDto.FromEntity).ToList();
        return Ok(bundleDtos);
    }

    [HttpGet("bundles/{bundleId:guid}")]
    public async Task<IActionResult> GetBundle(Guid bundleId)
    {
        var bundle = await _dbContext.DialogBundles
            .Include(b => b.Skill)
            .FirstOrDefaultAsync(b => b.Id == bundleId);

        if (bundle == null)
        {
            return NotFound(new { message = "Bundle not found" });
        }

        return Ok(DialogBundleDto.FromEntity(bundle));
    }

    [HttpPost("bundles")]
    public async Task<IActionResult> CreateBundle([FromBody] CreateBundleRequestDto request)
    {
        var skillExists = await _dbContext.Skills.AnyAsync(s => s.Id == request.SkillId);
        if (!skillExists)
        {
            return BadRequest(new { message = "Skill not found" });
        }

        var bundle = new DialogBundle
        {
            SkillId = request.SkillId,
            Title = request.Title,
            Description = request.Description,
            IconEmoji = request.IconEmoji,
            SortOrder = request.SortOrder,
            IsActive = request.IsActive
        };

        _dbContext.DialogBundles.Add(bundle);
        await _dbContext.SaveChangesAsync();

        await _dbContext.Entry(bundle).Reference(b => b.Skill).LoadAsync();

        _logger.LogInformation("Created dialog bundle {BundleId}: {Title}", bundle.Id, bundle.Title);

        return CreatedAtAction(nameof(GetBundle), new { bundleId = bundle.Id }, DialogBundleDto.FromEntity(bundle));
    }

    [HttpPut("bundles/{bundleId:guid}")]
    public async Task<IActionResult> UpdateBundle(Guid bundleId, [FromBody] UpdateBundleRequestDto request)
    {
        var bundle = await _dbContext.DialogBundles
            .Include(b => b.Skill)
            .FirstOrDefaultAsync(b => b.Id == bundleId);

        if (bundle == null)
        {
            return NotFound(new { message = "Bundle not found" });
        }

        if (request.SkillId.HasValue)
        {
            var skillExists = await _dbContext.Skills.AnyAsync(s => s.Id == request.SkillId.Value);
            if (!skillExists)
            {
                return BadRequest(new { message = "Skill not found" });
            }
            bundle.SkillId = request.SkillId.Value;
        }

        if (request.Title != null) bundle.Title = request.Title;
        if (request.Description != null) bundle.Description = request.Description;
        if (request.IconEmoji != null) bundle.IconEmoji = request.IconEmoji;
        if (request.SortOrder.HasValue) bundle.SortOrder = request.SortOrder.Value;
        if (request.IsActive.HasValue) bundle.IsActive = request.IsActive.Value;

        bundle.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync();

        await _dbContext.Entry(bundle).Reference(b => b.Skill).LoadAsync();

        _logger.LogInformation("Updated dialog bundle {BundleId}", bundleId);

        return Ok(DialogBundleDto.FromEntity(bundle));
    }

    [HttpDelete("bundles/{bundleId:guid}")]
    public async Task<IActionResult> DeleteBundle(Guid bundleId)
    {
        var bundle = await _dbContext.DialogBundles.FindAsync(bundleId);

        if (bundle == null)
        {
            return NotFound(new { message = "Bundle not found" });
        }

        _dbContext.DialogBundles.Remove(bundle);
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("Deleted dialog bundle {BundleId}", bundleId);

        return NoContent();
    }

    [HttpGet("bundles/{bundleId:guid}/modes")]
    public async Task<IActionResult> GetModesForBundle(Guid bundleId)
    {
        var modes = await _dbContext.DialogModes
            .Where(mode => mode.BundleId == bundleId)
            .OrderBy(mode => mode.SortOrder)
            .ToListAsync();

        var modeDtos = modes.Select(AdminDialogModeDto.FromEntity).ToList();
        return Ok(modeDtos);
    }

    [HttpPost("bundles/{bundleId:guid}/modes")]
    public async Task<IActionResult> CreateMode(Guid bundleId, [FromBody] CreateModeRequestDto request)
    {
        var bundleExists = await _dbContext.DialogBundles.AnyAsync(b => b.Id == bundleId);
        if (!bundleExists)
        {
            return NotFound(new { message = "Bundle not found" });
        }

        var mode = new DialogMode
        {
            BundleId = bundleId,
            Key = request.Key,
            Title = request.Title,
            Description = request.Description,
            ChatSystemPrompt = request.ChatSystemPrompt,
            FeedbackSystemPrompt = request.FeedbackSystemPrompt,
            SortOrder = request.SortOrder,
            IsActive = request.IsActive,
            VoiceEnabled = request.VoiceEnabled,
            VoiceId = request.VoiceId
        };

        _dbContext.DialogModes.Add(mode);
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("Created dialog mode {ModeId}: {Title} for bundle {BundleId}", mode.Id, mode.Title, bundleId);

        return CreatedAtAction(nameof(GetMode), new { modeId = mode.Id }, AdminDialogModeDto.FromEntity(mode));
    }

    [HttpGet("modes/{modeId:guid}")]
    public async Task<IActionResult> GetMode(Guid modeId)
    {
        var mode = await _dbContext.DialogModes.FindAsync(modeId);

        if (mode == null)
        {
            return NotFound(new { message = "Mode not found" });
        }

        return Ok(AdminDialogModeDto.FromEntity(mode));
    }

    [HttpPut("modes/{modeId:guid}")]
    public async Task<IActionResult> UpdateMode(Guid modeId, [FromBody] UpdateModeRequestDto request)
    {
        var mode = await _dbContext.DialogModes.FindAsync(modeId);

        if (mode == null)
        {
            return NotFound(new { message = "Mode not found" });
        }

        if (request.Key != null) mode.Key = request.Key;
        if (request.Title != null) mode.Title = request.Title;
        if (request.Description != null) mode.Description = request.Description;
        if (request.ChatSystemPrompt != null) mode.ChatSystemPrompt = request.ChatSystemPrompt;
        if (request.FeedbackSystemPrompt != null) mode.FeedbackSystemPrompt = request.FeedbackSystemPrompt;
        if (request.SortOrder.HasValue) mode.SortOrder = request.SortOrder.Value;
        if (request.IsActive.HasValue) mode.IsActive = request.IsActive.Value;
        if (request.VoiceEnabled.HasValue) mode.VoiceEnabled = request.VoiceEnabled.Value;
        if (request.VoiceId != null) mode.VoiceId = request.VoiceId;

        mode.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("Updated dialog mode {ModeId}", modeId);

        return Ok(AdminDialogModeDto.FromEntity(mode));
    }

    [HttpDelete("modes/{modeId:guid}")]
    public async Task<IActionResult> DeleteMode(Guid modeId)
    {
        var mode = await _dbContext.DialogModes.FindAsync(modeId);

        if (mode == null)
        {
            return NotFound(new { message = "Mode not found" });
        }

        _dbContext.DialogModes.Remove(mode);
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("Deleted dialog mode {ModeId}", modeId);

        return NoContent();
    }
}
