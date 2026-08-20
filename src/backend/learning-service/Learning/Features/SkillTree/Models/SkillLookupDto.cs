namespace Sellevate.Learning.Features.SkillTree.Models;

/// <summary>
/// C-3 audit fix. What ai-service needs to label a skill it only knows by id — its own dialog
/// bundles carry a <c>SkillId</c> and nothing else, because the skill itself is authored and owned
/// here, not in ai-service (docs/AUDIT_CONTRACTS.md, finding C-3).
/// </summary>
/// <param name="Id">The skill's id, as stored on the caller's own foreign key.</param>
/// <param name="IconicName">The skill's slug.</param>
/// <param name="Title">The skill's display title.</param>
public sealed record SkillLookupDto(Guid Id, string IconicName, string Title);
