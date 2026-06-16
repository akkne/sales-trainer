namespace SalesTrainer.Api.Features.League.Models;

/// <summary>
/// A configurable league tier (e.g. bronze, silver). Replaces the previously
/// hardcoded tier list: <see cref="Key"/> is the stable slug stored on
/// <see cref="League.Tier"/>, while <see cref="Name"/>/<see cref="Color"/> drive
/// presentation and <see cref="Order"/> defines the promotion ladder
/// (ascending — lowest order is the entry tier, highest is the top tier).
/// </summary>
public sealed class LeagueTier
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Color { get; set; } = string.Empty;
    public int Order { get; set; }
}
