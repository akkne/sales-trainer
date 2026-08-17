using Sellevate.Learning.Features.Assignments.Services.Abstract;
using Sellevate.Learning.Features.Assignments.Services.Implementation;

namespace Sellevate.Learning.Features.Assignments;

public static class AssignmentServiceCollectionExtensions
{
    public static IServiceCollection AddAssignmentFeatureServices(this IServiceCollection services)
    {
        services.AddScoped<IAssignmentService, AssignmentService>();
        services.AddScoped<IAssignmentThresholdEvaluator, AssignmentThresholdEvaluator>();
        services.AddScoped<IAssignmentAudienceResolver, AssignmentAudienceResolver>();
        services.AddScoped<IMyAssignmentService, MyAssignmentService>();

        return services;
    }
}
