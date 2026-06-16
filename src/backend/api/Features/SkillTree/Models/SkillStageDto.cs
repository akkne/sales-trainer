namespace SalesTrainer.Api.Features.SkillTree.Models;

/// <summary>Public, read-only view of a configurable skill-tree funnel stage.</summary>
public sealed record SkillStageDto(string Key, string Label, string Accent, int Order);
