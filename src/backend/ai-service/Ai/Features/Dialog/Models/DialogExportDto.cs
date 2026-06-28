namespace Sellevate.Ai.Features.Dialog.Models;

/// <summary>
/// Full dialog export: bundles with their nested modes, shaped to round-trip
/// through <c>POST /admin/dialog/import</c> (re-importable verbatim).
/// </summary>
public sealed record DialogExportDto(IReadOnlyList<DialogBundleExportDto> Bundles);

public sealed record DialogBundleExportDto(
    Guid SkillId,
    string Title,
    string Description,
    string IconEmoji,
    int SortOrder,
    bool IsActive,
    IReadOnlyList<DialogModeExportDto> Modes);

public sealed record DialogModeExportDto(
    string Key,
    string Title,
    string Description,
    string ChatSystemPrompt,
    string FeedbackSystemPrompt,
    int SortOrder,
    bool IsActive,
    bool VoiceEnabled,
    string? VoiceId);
