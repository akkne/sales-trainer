namespace SalesTrainer.Api.Features.Techniques.Models;

public sealed class TechniqueCoach
{
    public Guid Id { get; set; }
    public Guid TechniqueId { get; set; }
    public string AvatarSeed { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string Quote { get; set; } = string.Empty;
    public string? ChallengesJson { get; set; }
}
