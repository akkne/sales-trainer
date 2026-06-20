namespace Sellevate.Learning.Features.Admin.Models;

public sealed record AdminSkillStageDto(Guid Id, string Key, string Label, string Accent, int Order);

public sealed record CreateSkillStageRequestDto(string? Key, string? Label, string? Accent, int Order);

public sealed record UpdateSkillStageRequestDto(string? Label, string? Accent, int Order);
