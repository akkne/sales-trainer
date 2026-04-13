namespace SalesTrainer.Api.Features.Onboarding;

public static class OnboardingServiceCollectionExtensions
{
    public static IServiceCollection AddOnboardingFeatureServices(this IServiceCollection services)
    {
        services.AddScoped<IOnboardingService, OnboardingService>();
        return services;
    }
}
