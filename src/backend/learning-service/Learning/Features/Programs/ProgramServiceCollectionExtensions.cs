using Sellevate.Learning.Features.Programs.Services.Abstract;
using Sellevate.Learning.Features.Programs.Services.Implementation;

namespace Sellevate.Learning.Features.Programs;

public static class ProgramServiceCollectionExtensions
{
    public static IServiceCollection AddProgramFeatureServices(this IServiceCollection services)
    {
        services.AddScoped<IProgramVersionService, ProgramVersionService>();
        services.AddScoped<IProgramEnrollmentService, ProgramEnrollmentService>();

        return services;
    }
}
