namespace Sellevate.Ai.Infrastructure.Learning;

/// <summary>
/// C-3 audit fix. A skill's slug and display title, as learning-service names it — the two fields
/// a <c>DialogBundleDto</c> needs to label the skill it only knows by id.
/// </summary>
public sealed record SkillSummary(string Slug, string Title);
