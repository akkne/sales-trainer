namespace SalesTrainer.Api.Features.Reference;

public class ReferenceMaterial
{
    public Guid Id { get; set; }
    public Guid SkillId { get; set; }
    public string Title { get; set; } = "";
    public string MarkdownContent { get; set; } = "";
    public int SortOrder { get; set; }
    /// <summary>Category slug, e.g. "objections", "cold-calls", "closing"</summary>
    public string? Category { get; set; }
    /// <summary>Comma-separated tags, e.g. "rapport,discovery"</summary>
    public string? Tags { get; set; }
}
