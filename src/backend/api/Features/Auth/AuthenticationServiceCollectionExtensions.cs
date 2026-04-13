namespace SalesTrainer.Api.Features.Auth;

public static class AuthenticationServiceCollectionExtensions
{
    public static IServiceCollection AddAuthenticationFeatureServices(this IServiceCollection services)
    {
        services.AddScoped<IAuthenticationService, AuthenticationService>();
        services.AddScoped<SuperAdminSeeder>();
        return services;
    }
}
