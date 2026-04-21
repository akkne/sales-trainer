namespace SalesTrainer.Api.Features.Techniques.Models;

public sealed class TechniqueCategory
{
    public string Slug { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Color { get; set; } = string.Empty;
    public int SortOrder { get; set; }
}
