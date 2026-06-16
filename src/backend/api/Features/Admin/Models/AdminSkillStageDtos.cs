namespace SalesTrainer.Api.Features.Admin.Models;

public sealed record AdminSkillStageDto(Guid Id, string Key, string Label, string Accent, int Order);

public sealed record CreateSkillStageRequestDto(string? Key, string? Label, string? Accent, int Order);

/// <summary>
/// The key (slug) is immutable: it is stored on every Skill row, so renaming it
/// would orphan the grouping. Only label, accent, and order are editable.
/// </summary>
public sealed record UpdateSkillStageRequestDto(string? Label, string? Accent, int Order);
