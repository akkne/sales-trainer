namespace SalesTrainer.Api.Features.Reference;

public class ReferenceMaterial
{
    public Guid Id { get; set; }
    public Guid SkillId { get; set; }
    public string Title { get; set; } = "";
    public string MarkdownContent { get; set; } = "";
    public int SortOrder { get; set; }
}
