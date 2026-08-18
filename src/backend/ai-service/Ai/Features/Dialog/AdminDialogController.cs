using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sellevate.Ai.Features.Dialog.Constants;
using Sellevate.Ai.Features.Dialog.Models;
using Sellevate.Ai.Infrastructure.Data;
using Sellevate.Ai.Common.Constants;

namespace Sellevate.Ai.Features.Dialog;

/// <summary>
/// Phase 40.6 audit: authors the shared dialog-content library — bundles, modes and their prompts.
/// Sellevate-staff-only.
///
/// <para>
/// <b>Platform-only, and a separate controller from the override surface.</b> Editing an organization's
/// own copy happens on <see cref="Overrides.AdminDialogOverridesController"/>. That split is deliberate
/// rather than cosmetic: stacking a second <c>[Authorize]</c> on an action here would AND the two
/// policies, not OR them, so an organization administrator would still be refused while the code read
/// as if they were allowed. Two controllers make the weaker gate impossible to misread.
/// </para>
///
/// <para>
/// <c>[TenantTransaction]</c> closes ai-service's long-standing gap (docs/DONT_FORGET.md):
/// <c>SET LOCAL</c> only takes effect inside a transaction, so without it an organization's own
/// override is invisible even to a platform administrator working inside that organization.
/// </para>
///
/// <para>
/// <b>Import is partially tolerant on purpose.</b> A malformed bundle or mode is collected into the
/// error list and the rest of the file is still applied, because an operator importing sixty modes
/// wants the fifty-nine good ones in and a list of what to fix — not an all-or-nothing rejection.
/// Matching is by natural key (skill plus title, bundle plus mode key), so re-importing the same file
/// updates rather than duplicates.
/// </para>
/// </summary>
[ApiController]
[Route("admin/dialog")]
[TenantTransaction]
[Authorize(Policy = AuthorizationPolicies.RequirePlatformAdministrator)]
public sealed class AdminDialogController : ControllerBase
{
    /// <summary>
    /// Kestrel's body ceiling for a content import. A compile-time constant because
    /// <c>[RequestSizeLimit]</c> is an attribute argument and cannot read configuration.
    /// </summary>
    private const int MaximumImportFileSizeBytes = 20 * 1024 * 1024;

    private const string DefaultBundleIconEmoji = "💬";
    private const string JsonFileExtension = ".json";

    private readonly AiDbContext _databaseContext;
    private readonly ILogger<AdminDialogController> _logger;

    public AdminDialogController(AiDbContext databaseContext, ILogger<AdminDialogController> logger)
    {
        _databaseContext = databaseContext;
        _logger = logger;
    }

    [HttpGet("bundles")]
    public async Task<IActionResult> GetAllBundles()
    {
        var bundles = await _databaseContext.DialogBundles
            .OrderBy(bundle => bundle.SortOrder)
            .ToListAsync();

        var bundleDtos = bundles.Select(DialogBundleDto.FromEntity).ToList();
        return Ok(bundleDtos);
    }

    [HttpGet("bundles/{bundleId:guid}")]
    public async Task<IActionResult> GetBundle(Guid bundleId)
    {
        var bundle = await _databaseContext.DialogBundles
            .FirstOrDefaultAsync(bundle => bundle.Id == bundleId);

        if (bundle == null)
        {
            return NotFound(new { message = DialogMessages.BundleNotFound });
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

        _databaseContext.DialogBundles.Add(bundle);
        await _databaseContext.SaveChangesAsync();

        _logger.LogInformation("Created dialog bundle {BundleId}: {Title}", bundle.Id, bundle.Title);

        return CreatedAtAction(nameof(GetBundle), new { bundleId = bundle.Id }, DialogBundleDto.FromEntity(bundle));
    }

    [HttpPut("bundles/{bundleId:guid}")]
    public async Task<IActionResult> UpdateBundle(Guid bundleId, [FromBody] UpdateBundleRequestDto request)
    {
        var bundle = await _databaseContext.DialogBundles
            .FirstOrDefaultAsync(bundle => bundle.Id == bundleId);

        if (bundle == null)
        {
            return NotFound(new { message = DialogMessages.BundleNotFound });
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
        if (request.IsHidden.HasValue) bundle.IsHidden = request.IsHidden.Value;

        bundle.UpdatedAt = DateTime.UtcNow;

        await _databaseContext.SaveChangesAsync();

        _logger.LogInformation("Updated dialog bundle {BundleId}", bundleId);

        return Ok(DialogBundleDto.FromEntity(bundle));
    }

    [HttpDelete("bundles/{bundleId:guid}")]
    public async Task<IActionResult> DeleteBundle(Guid bundleId)
    {
        var bundle = await _databaseContext.DialogBundles.FindAsync(bundleId);

        if (bundle == null)
        {
            return NotFound(new { message = DialogMessages.BundleNotFound });
        }

        _databaseContext.DialogBundles.Remove(bundle);
        await _databaseContext.SaveChangesAsync();

        _logger.LogInformation("Deleted dialog bundle {BundleId}", bundleId);

        return NoContent();
    }

    [HttpGet("export")]
    public async Task<ActionResult<DialogExportDto>> Export(CancellationToken cancellationToken)
    {
        var bundles = await _databaseContext.DialogBundles
            .OrderBy(bundle => bundle.SortOrder)
            .ToListAsync(cancellationToken);

        var modesByBundle = (await _databaseContext.DialogModes
                .OrderBy(mode => mode.SortOrder)
                .ToListAsync(cancellationToken))
            .GroupBy(mode => mode.BundleId)
            .ToDictionary(group => group.Key, group => group.ToList());

        var bundleDtos = bundles.Select(bundle => new DialogBundleExportDto(
            bundle.SkillId,
            bundle.Title,
            bundle.Description,
            bundle.IconEmoji,
            bundle.SortOrder,
            bundle.IsActive,
            (modesByBundle.TryGetValue(bundle.Id, out var modes) ? modes : new List<DialogMode>())
                .Select(mode => new DialogModeExportDto(
                    mode.Key,
                    mode.Title,
                    mode.Description,
                    mode.ChatSystemPrompt,
                    mode.FeedbackSystemPrompt,
                    mode.SortOrder,
                    mode.IsActive,
                    mode.VoiceEnabled,
                    mode.VoiceId))
                .ToList()))
            .ToList();

        _logger.LogInformation(
            "Dialog export: Bundles={BundleCount} Modes={ModeCount} by ActorId={ActorId}",
            bundleDtos.Count, modesByBundle.Values.Sum(list => list.Count),
            User.FindFirstValue(ClaimTypes.NameIdentifier));

        return Ok(new DialogExportDto(bundleDtos));
    }

    [HttpPost("import")]
    [RequestSizeLimit(MaximumImportFileSizeBytes)]
    public async Task<ActionResult<DialogImportResultDto>> Import(IFormFile file)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { message = "File is required." });
        if (!file.FileName.EndsWith(JsonFileExtension, StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { message = "Only .json files are accepted." });

        var existingBundles = await _databaseContext.DialogBundles.ToListAsync();
        var existingModes = await _databaseContext.DialogModes.ToListAsync();

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
            if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("bundles", out var bundlesProperty))
                root = bundlesProperty;
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
                    var iconEmoji = ReadString(bundleElement, "iconEmoji") ?? DefaultBundleIconEmoji;
                    var sortOrder = bundleElement.TryGetProperty("sortOrder", out var sortOrderProperty) ? sortOrderProperty.GetInt32() : 0;
                    var isActive = !bundleElement.TryGetProperty("isActive", out var isActiveProperty) || isActiveProperty.GetBoolean();

                    var existingBundle = existingBundles.FirstOrDefault(candidate => candidate.SkillId == skillId && candidate.Title == bundleTitle);
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
                        _databaseContext.DialogBundles.Add(bundle);
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
                        var sortOrder = modeElement.TryGetProperty("sortOrder", out var sortOrderProperty) ? sortOrderProperty.GetInt32() : 0;
                        var isActive = !modeElement.TryGetProperty("isActive", out var isActiveProperty) || isActiveProperty.GetBoolean();
                        var voiceEnabled = modeElement.TryGetProperty("voiceEnabled", out var voiceEnabledProperty) && voiceEnabledProperty.GetBoolean();
                        var voiceId = ReadString(modeElement, "voiceId");

                        var existingMode = existingModes.FirstOrDefault(candidate => candidate.BundleId == bundle.Id && candidate.Key == key);
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
                            _databaseContext.DialogModes.Add(mode);
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

        await _databaseContext.SaveChangesAsync();

        _logger.LogInformation(
            "Dialog import: BundlesCreated={BundlesCreated} BundlesUpdated={BundlesUpdated} "
            + "ModesCreated={ModesCreated} ModesUpdated={ModesUpdated} Errors={ErrorCount} by ActorId={ActorId}",
            bundlesCreated, bundlesUpdated, modesCreated, modesUpdated, errors.Count,
            User.FindFirstValue(ClaimTypes.NameIdentifier));

        return Ok(new DialogImportResultDto(bundlesCreated, bundlesUpdated, modesCreated, modesUpdated, errors));
    }

    private static string? ReadString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;

    [HttpGet("bundles/{bundleId:guid}/modes")]
    public async Task<IActionResult> GetModesForBundle(Guid bundleId)
    {
        var modes = await _databaseContext.DialogModes
            .Where(mode => mode.BundleId == bundleId)
            .OrderBy(mode => mode.SortOrder)
            .ToListAsync();

        var modeDtos = modes.Select(AdminDialogModeDto.FromEntity).ToList();
        return Ok(modeDtos);
    }

    [HttpPost("bundles/{bundleId:guid}/modes")]
    public async Task<IActionResult> CreateMode(Guid bundleId, [FromBody] CreateModeRequestDto request)
    {
        var bundleExists = await _databaseContext.DialogBundles.AnyAsync(bundle => bundle.Id == bundleId);
        if (!bundleExists)
        {
            return NotFound(new { message = DialogMessages.BundleNotFound });
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

        _databaseContext.DialogModes.Add(mode);
        await _databaseContext.SaveChangesAsync();

        _logger.LogInformation("Created dialog mode {ModeId}: {Title} for bundle {BundleId}", mode.Id, mode.Title, bundleId);

        return CreatedAtAction(nameof(GetMode), new { modeId = mode.Id }, AdminDialogModeDto.FromEntity(mode));
    }

    [HttpGet("modes/{modeId:guid}")]
    public async Task<IActionResult> GetMode(Guid modeId)
    {
        var mode = await _databaseContext.DialogModes.FindAsync(modeId);

        if (mode == null)
        {
            return NotFound(new { message = DialogMessages.ModeNotFound });
        }

        return Ok(AdminDialogModeDto.FromEntity(mode));
    }

    [HttpPut("modes/{modeId:guid}")]
    public async Task<IActionResult> UpdateMode(Guid modeId, [FromBody] UpdateModeRequestDto request)
    {
        var mode = await _databaseContext.DialogModes.FindAsync(modeId);

        if (mode == null)
        {
            return NotFound(new { message = DialogMessages.ModeNotFound });
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

        await _databaseContext.SaveChangesAsync();

        _logger.LogInformation("Updated dialog mode {ModeId}", modeId);

        return Ok(AdminDialogModeDto.FromEntity(mode));
    }

    [HttpDelete("modes/{modeId:guid}")]
    public async Task<IActionResult> DeleteMode(Guid modeId)
    {
        var mode = await _databaseContext.DialogModes.FindAsync(modeId);

        if (mode == null)
        {
            return NotFound(new { message = DialogMessages.ModeNotFound });
        }

        _databaseContext.DialogModes.Remove(mode);
        await _databaseContext.SaveChangesAsync();

        _logger.LogInformation("Deleted dialog mode {ModeId}", modeId);

        return NoContent();
    }
}
