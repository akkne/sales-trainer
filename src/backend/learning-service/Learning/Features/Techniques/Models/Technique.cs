namespace Sellevate.Learning.Features.Techniques.Models;

public sealed class Technique
{
    public Guid Id { get; set; }

    /// <summary>
    /// Phase 40.10. <see langword="null"/> means the row belongs to the global content library
    /// shared by every organization; a non-null value means one organization's own copy
    /// (docs/TENANCY/CONTENT_MODEL.md). Written since 40.18 by copy-on-write override creation.
    /// </summary>
    public Guid? OrganizationId { get; set; }

    /// <summary>
    /// Phase 40.18. Set when this row is one organization's copy-on-write override of a global
    /// technique; null for a technique that stands on its own. A row with a parent always has an
    /// owning organization — a database CHECK says so, because a "global override of a global row"
    /// would make read resolution shadow the library with itself.
    /// </summary>
    public Guid? ParentTechniqueId { get; set; }

    /// <summary>
    /// Phase 40.18. Lowercase hex SHA-256 of the parent's canonical content at the moment this
    /// override was forked or last reviewed — the provenance pointer <c>LessonVersion.BaseVersionId</c>
    /// is for lessons. Techniques have no immutable version table, so the fork point is recorded as
    /// a fingerprint of the base rather than as the id of a frozen snapshot: the staleness question
    /// ("did upstream move since we forked?") is answerable either way, and building a second
    /// version table was out of 40.18's scope (docs/DECISIONS.md, 2026-08-18).
    /// </summary>
    public string? BaseContentHash { get; set; }

    /// <summary>
    /// Phase 40.18. "Take the new base" retires the override instead of deleting it: user technique
    /// progress points at this row without a foreign key, and deleting the row would orphan that
    /// history to make a review action tidy. Retired overrides stop shadowing their parent, so the
    /// global technique becomes visible again — which is exactly what accepting the base means.
    /// </summary>
    public bool IsArchived { get; set; }

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
