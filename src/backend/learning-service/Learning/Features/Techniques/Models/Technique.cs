namespace Sellevate.Learning.Features.Techniques.Models;

public sealed class Technique
{
    public Guid Id { get; set; }

    /// <summary>
    /// Phase 40.10. <see langword="null"/> means the row belongs to the global content library
    /// shared by every organization; a non-null value means one organization's own copy
    /// (docs/TENANCY/CONTENT_MODEL.md). Nothing writes a non-null value yet — content authoring
    /// is superadmin-only until 40.18 — but the query filter and the RLS policy already read it.
    /// </summary>
    public Guid? OrganizationId { get; set; }

    public string Slug { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string[] Tags { get; set; } = Array.Empty<string>();
    public Guid? PrimarySkillId { get; set; }
    public int Difficulty { get; set; } = TechniqueLevels.Novice;
    public string? DialogJson { get; set; }
    public string? CaseJson { get; set; }
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public List<TechniqueSkill> AdditionalSkills { get; set; } = new();
    public TechniqueCoach? Coach { get; set; }
}
