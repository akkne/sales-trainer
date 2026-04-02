namespace SalesTrainer.Api.Features.Onboarding;

public class UserProfile
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string SalesType { get; set; } = "";
    public string ExperienceLevel { get; set; } = "";
    public string Goal { get; set; } = "";
    public bool IsOnboardingCompleted { get; set; }
}
