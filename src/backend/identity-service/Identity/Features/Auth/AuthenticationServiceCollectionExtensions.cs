using Sellevate.Identity.Features.Auth.Services.Abstract;
using Sellevate.Identity.Features.Auth.Services.Implementation;
using Sellevate.Identity.Infrastructure.Configuration;

namespace Sellevate.Identity.Features.Auth;

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
        services.AddScoped<IGoogleTokenValidator, GoogleTokenValidator>();
        // Phase 40.8 seam: providers are resolved as a collection and selected by
        // OrganizationAuthConfiguration.Method. Adding OIDC/SAML later is one more registration
        // here plus its implementation — nothing in the login flow changes.
        services.AddScoped<IAuthProvider, PasswordAuthProvider>();
        services.AddScoped<IOrganizationAuthConfigurationResolver, OrganizationAuthConfigurationResolver>();
        services.AddScoped<IAuthenticationService, AuthenticationService>();
        services.AddScoped<IEmailVerificationService, EmailVerificationService>();
        services.AddScoped<SuperAdminSeeder>();
        services.AddHostedService<ExpiredEmailVerificationCleanupService>();
        services.AddHostedService<ExpiredRefreshTokenCleanupService>();
        return services;
    }
}
