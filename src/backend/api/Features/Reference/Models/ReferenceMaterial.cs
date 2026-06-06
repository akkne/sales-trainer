namespace SalesTrainer.Api.Features.Reference.Models;

public sealed class ReferenceMaterial
{
    public Guid Id { get; set; }
    public Guid SkillId { get; set; }
    public string Title { get; set; } = "";
    public string MarkdownContent { get; set; } = "";
    public int SortOrder { get; set; }
    public string? Category { get; set; }
    public string? Tags { get; set; }
}
