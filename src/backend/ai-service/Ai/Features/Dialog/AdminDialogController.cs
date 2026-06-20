using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sellevate.Ai.Features.Dialog.Models;
using Sellevate.Ai.Infrastructure.Data;

namespace Sellevate.Ai.Features.Dialog;

[ApiController]
[Route("admin/dialog")]
[Authorize(Policy = "RequireAdmin")]
public sealed class AdminDialogController : ControllerBase
{
    private readonly AiDbContext _dbContext;
    private readonly ILogger<AdminDialogController> _logger;

    public AdminDialogController(AiDbContext dbContext, ILogger<AdminDialogController> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    [HttpGet("bundles")]
    public async Task<IActionResult> GetAllBundles()
    {
        var bundles = await _dbContext.DialogBundles
            .OrderBy(bundle => bundle.SortOrder)
            .ToListAsync();

        var bundleDtos = bundles.Select(DialogBundleDto.FromEntity).ToList();
        return Ok(bundleDtos);
    }

    [HttpGet("bundles/{bundleId:guid}")]
    public async Task<IActionResult> GetBundle(Guid bundleId)
    {
        var bundle = await _dbContext.DialogBundles
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

        _logger.LogInformation("Created dialog bundle {BundleId}: {Title}", bundle.Id, bundle.Title);

        return CreatedAtAction(nameof(GetBundle), new { bundleId = bundle.Id }, DialogBundleDto.FromEntity(bundle));
    }

    [HttpPut("bundles/{bundleId:guid}")]
    public async Task<IActionResult> UpdateBundle(Guid bundleId, [FromBody] UpdateBundleRequestDto request)
    {
        var bundle = await _dbContext.DialogBundles
            .FirstOrDefaultAsync(b => b.Id == bundleId);

        if (bundle == null)
        {
            return NotFound(new { message = "Bundle not found" });
        }

        if (request.SkillId.HasValue)
        {
            bundle.SkillId = request.SkillId.Value;
        }

        if (request.Title != null) bundle.Title = request.Title;
        if (request.Description != null) bundle.Description = request.Description;
        if (request.IconEmoji != null) bundle.IconEmoji = request.IconEmoji;
        if (request.SortOrder.HasValue) bundle.SortOrder = request.SortOrder.Value;
        if (request.IsActive.HasValue) bundle.IsActive = request.IsActive.Value;

        bundle.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync();

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

    [HttpPost("import")]
    [RequestSizeLimit(20 * 1024 * 1024)]
    public async Task<ActionResult<DialogImportResultDto>> Import(IFormFile file)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { message = "File is required." });
        if (!file.FileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { message = "Only .json files are accepted." });

        var existingBundles = await _dbContext.DialogBundles.ToListAsync();
        var existingModes = await _dbContext.DialogModes.ToListAsync();

        var bundlesCreated = 0;
        var bundlesUpdated = 0;
        var modesCreated = 0;
        var modesUpdated = 0;
        var errors = new List<string>();
        var now = DateTime.UtcNow;

        try
        {
            using var document = await JsonDocument.ParseAsync(file.OpenReadStream());
            var root = document.RootElement;
            if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("bundles", out var bundlesProp))
                root = bundlesProp;
            if (root.ValueKind != JsonValueKind.Array)
                return BadRequest(new { message = "JSON must be an object { \"bundles\": [...] } or an array of bundle objects." });

            foreach (var (bundleElement, bundleIndex) in root.EnumerateArray().Select((e, i) => (e, i + 1)))
            {
                var bundleTitle = "";
                DialogBundle bundle;
                try
                {
                    var skillIdRaw = ReadString(bundleElement, "skillId")?.Trim() ?? "";
                    if (!Guid.TryParse(skillIdRaw, out var skillId))
                    {
                        errors.Add($"Bundle {bundleIndex}: 'skillId' is missing or not a valid GUID.");
                        continue;
                    }

                    bundleTitle = bundleElement.GetProperty("title").GetString()?.Trim() ?? "";
                    if (string.IsNullOrWhiteSpace(bundleTitle))
                        throw new InvalidOperationException("title is empty.");

                    var description = ReadString(bundleElement, "description") ?? "";
                    var iconEmoji = ReadString(bundleElement, "iconEmoji") ?? "💬";
                    var sortOrder = bundleElement.TryGetProperty("sortOrder", out var soProp) ? soProp.GetInt32() : 0;
                    var isActive = !bundleElement.TryGetProperty("isActive", out var iaProp) || iaProp.GetBoolean();

                    var existingBundle = existingBundles.FirstOrDefault(b => b.SkillId == skillId && b.Title == bundleTitle);
                    if (existingBundle is not null)
                    {
                        existingBundle.Description = description;
                        existingBundle.IconEmoji = iconEmoji;
                        existingBundle.SortOrder = sortOrder;
                        existingBundle.IsActive = isActive;
                        existingBundle.UpdatedAt = now;
                        bundle = existingBundle;
                        bundlesUpdated++;
                    }
                    else
                    {
                        bundle = new DialogBundle
                        {
                            Id = Guid.NewGuid(),
                            SkillId = skillId,
                            Title = bundleTitle,
                            Description = description,
                            IconEmoji = iconEmoji,
                            SortOrder = sortOrder,
                            IsActive = isActive,
                            CreatedAt = now,
                            UpdatedAt = now
                        };
                        _dbContext.DialogBundles.Add(bundle);
                        existingBundles.Add(bundle);
                        bundlesCreated++;
                    }
                }
                catch (Exception exception)
                {
                    errors.Add($"Bundle {bundleIndex} ('{bundleTitle}'): {exception.Message}");
                    continue;
                }

                if (!bundleElement.TryGetProperty("modes", out var modesElement) || modesElement.ValueKind != JsonValueKind.Array)
                    continue;

                foreach (var (modeElement, modeIndex) in modesElement.EnumerateArray().Select((e, i) => (e, i + 1)))
                {
                    try
                    {
                        var key = modeElement.GetProperty("key").GetString()?.Trim() ?? "";
                        if (string.IsNullOrWhiteSpace(key))
                            throw new InvalidOperationException("key is empty.");
                        var modeTitle = modeElement.GetProperty("title").GetString()?.Trim() ?? "";
                        if (string.IsNullOrWhiteSpace(modeTitle))
                            throw new InvalidOperationException("title is empty.");

                        var description = ReadString(modeElement, "description") ?? "";
                        var chatPrompt = ReadString(modeElement, "chatSystemPrompt") ?? "";
                        var feedbackPrompt = ReadString(modeElement, "feedbackSystemPrompt") ?? "";
                        var sortOrder = modeElement.TryGetProperty("sortOrder", out var soProp) ? soProp.GetInt32() : 0;
                        var isActive = !modeElement.TryGetProperty("isActive", out var iaProp) || iaProp.GetBoolean();
                        var voiceEnabled = modeElement.TryGetProperty("voiceEnabled", out var veProp) && veProp.GetBoolean();
                        var voiceId = ReadString(modeElement, "voiceId");

                        var existingMode = existingModes.FirstOrDefault(m => m.BundleId == bundle.Id && m.Key == key);
                        if (existingMode is not null)
                        {
                            existingMode.Title = modeTitle;
                            existingMode.Description = description;
                            existingMode.ChatSystemPrompt = chatPrompt;
                            existingMode.FeedbackSystemPrompt = feedbackPrompt;
                            existingMode.SortOrder = sortOrder;
                            existingMode.IsActive = isActive;
                            existingMode.VoiceEnabled = voiceEnabled;
                            existingMode.VoiceId = voiceId;
                            existingMode.UpdatedAt = now;
                            modesUpdated++;
                        }
                        else
                        {
                            var mode = new DialogMode
                            {
                                Id = Guid.NewGuid(),
                                BundleId = bundle.Id,
                                Key = key,
                                Title = modeTitle,
                                Description = description,
                                ChatSystemPrompt = chatPrompt,
                                FeedbackSystemPrompt = feedbackPrompt,
                                SortOrder = sortOrder,
                                IsActive = isActive,
                                VoiceEnabled = voiceEnabled,
                                VoiceId = voiceId,
                                CreatedAt = now,
                                UpdatedAt = now
                            };
                            _dbContext.DialogModes.Add(mode);
                            existingModes.Add(mode);
                            modesCreated++;
                        }
                    }
                    catch (Exception exception)
                    {
                        errors.Add($"Bundle '{bundleTitle}', mode {modeIndex}: {exception.Message}");
                    }
                }
            }
        }
        catch (JsonException exception) { return BadRequest(new { message = $"JSON parse error: {exception.Message}" }); }

        await _dbContext.SaveChangesAsync();

        _logger.LogInformation(
            "Dialog import: Bundles={BC}/{BU} Modes={MC}/{MU} Errors={ErrorCount} by ActorId={ActorId}",
            bundlesCreated, bundlesUpdated, modesCreated, modesUpdated, errors.Count,
            User.FindFirstValue(ClaimTypes.NameIdentifier));

        return Ok(new DialogImportResultDto(bundlesCreated, bundlesUpdated, modesCreated, modesUpdated, errors));
    }

    private static string? ReadString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.String
            ? prop.GetString()
            : null;

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
