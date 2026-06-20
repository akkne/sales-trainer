namespace Sellevate.Identity.Features.Onboarding.Models;

public sealed class UserProfile
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string SalesType { get; set; } = "";
    public string ExperienceLevel { get; set; } = "";
    public string Goal { get; set; } = "";
    public bool IsOnboardingCompleted { get; set; }
    public string? Persona { get; set; }
}
