using Sellevate.Company.Features.Companies.Services.Abstract;
using Sellevate.Company.Features.Companies.Services.Implementation;

namespace Sellevate.Company.Features.Companies;

public static class CompanyFeatureServiceCollectionExtensions
{
    public static IServiceCollection AddCompanyFeatureServices(this IServiceCollection services)
    {
        services.AddScoped<ICompanyService, CompanyService>();
        return services;
    }
}
