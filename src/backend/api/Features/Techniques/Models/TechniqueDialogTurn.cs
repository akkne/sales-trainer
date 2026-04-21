namespace SalesTrainer.Api.Features.Techniques.Models;

public sealed class TechniqueDialogTurn
{
    public Guid Id { get; set; }
    public Guid TechniqueId { get; set; }
    public int OrderIndex { get; set; }
    public string Side { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public string? AnnotationsJson { get; set; }
}
