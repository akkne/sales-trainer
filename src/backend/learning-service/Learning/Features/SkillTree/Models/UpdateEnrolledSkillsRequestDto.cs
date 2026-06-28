namespace Sellevate.Learning.Features.SkillTree.Models;

/// <summary>
/// Replaces the caller's enrolled skill set. <see cref="SkillSlugs"/> is the full
/// list of skill slugs (<see cref="Skill.IconicName"/>) the user wants enrolled;
/// the always-on core skill is kept by the backend regardless.
/// </summary>
public record UpdateEnrolledSkillsRequestDto(IReadOnlyList<string> SkillSlugs);
