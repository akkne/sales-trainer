using Sellevate.Identity.Features.Onboarding.Services.Abstract;
using Sellevate.Identity.Features.Onboarding.Services.Implementation;

namespace Sellevate.Identity.Features.Onboarding;

public static class OnboardingServiceCollectionExtensions
{
    public static IServiceCollection AddOnboardingFeatureServices(this IServiceCollection services)
    {
        services.AddScoped<IOnboardingService, OnboardingService>();
        return services;
    }
}
