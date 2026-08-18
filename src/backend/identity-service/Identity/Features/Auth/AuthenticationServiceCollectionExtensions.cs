using Sellevate.Identity.Features.Auth.Services.Abstract;
using Sellevate.Identity.Features.Auth.Services.Implementation;
using Sellevate.Identity.Infrastructure.Configuration;

namespace Sellevate.Identity.Features.Auth;

/// <summary>
/// Registers the authentication feature. <see cref="IAuthProvider"/> is deliberately registered as
/// a collection and selected at login time by <c>OrganizationAuthConfiguration.Method</c> (Phase
/// 40.8 seam): adding OIDC or SAML later is one more registration here plus its implementation, and
/// nothing in the login flow changes.
/// </summary>
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
        services.Configure<BackgroundJobConfiguration>(configuration.GetSection(BackgroundJobConfiguration.SectionName));
        services.AddScoped<IGoogleTokenValidator, GoogleTokenValidator>();
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
