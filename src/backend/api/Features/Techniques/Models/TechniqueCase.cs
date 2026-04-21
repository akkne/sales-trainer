namespace SalesTrainer.Api.Features.Techniques.Models;

public sealed class TechniqueCase
{
    public Guid Id { get; set; }
    public Guid TechniqueId { get; set; }
    public int OrderIndex { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string? MetricsJson { get; set; }
}
