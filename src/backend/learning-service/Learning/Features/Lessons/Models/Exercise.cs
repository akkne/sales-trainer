namespace Sellevate.Learning.Features.Lessons.Models;

public sealed class Exercise
{
    public Guid Id { get; set; }

    /// <summary>
    /// Phase 40.10. <see langword="null"/> means the row belongs to the global content library
    /// shared by every organization; a non-null value means one organization's own copy
    /// (docs/TENANCY/CONTENT_MODEL.md). Nothing writes a non-null value yet — content authoring
    /// is superadmin-only until 40.18 — but the query filter and the RLS policy already read it.
    /// </summary>
    public Guid? OrganizationId { get; set; }

    public Guid LessonId { get; set; }
    public string Type { get; set; } = "";
    public int OrderInLesson { get; set; }
    public string SerializedContent { get; set; } = "{}";
    public string? CustomAiPrompt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Lesson? Lesson { get; set; }
}
