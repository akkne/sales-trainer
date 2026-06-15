using SalesTrainer.Api.Features.Auth.Services.Abstract;
using SalesTrainer.Api.Features.Auth.Services.Implementation;
using SalesTrainer.Api.Infrastructure.Configuration;

namespace SalesTrainer.Api.Features.Auth;

public static class AuthenticationServiceCollectionExtensions
{
    public static IServiceCollection AddAuthenticationFeatureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<JwtConfiguration>(configuration.GetSection(JwtConfiguration.SectionName));
        services.Configure<SuperAdminConfiguration>(configuration.GetSection(SuperAdminConfiguration.SectionName));
        services.Configure<GoogleAuthConfiguration>(configuration.GetSection(GoogleAuthConfiguration.SectionName));
        services.Configure<EmailVerificationConfiguration>(configuration.GetSection(EmailVerificationConfiguration.SectionName));
        services.AddScoped<IAuthenticationService, AuthenticationService>();
        services.AddScoped<IEmailVerificationService, EmailVerificationService>();
        services.AddScoped<SuperAdminSeeder>();
        services.AddScoped<ExpiredEmailVerificationCleanupJob>();
        return services;
    }
}
