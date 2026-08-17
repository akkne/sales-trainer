using Sellevate.Learning.Features.Assignments.Services.Abstract;
using Sellevate.Learning.Features.Assignments.Services.Implementation;

namespace Sellevate.Learning.Features.Assignments;

public static class AssignmentServiceCollectionExtensions
{
    public static IServiceCollection AddAssignmentFeatureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<AssignmentOptions>(configuration.GetSection(AssignmentOptions.SectionName));

        services.AddScoped<IAssignmentService, AssignmentService>();
        services.AddScoped<IAssignmentThresholdEvaluator, AssignmentThresholdEvaluator>();
        services.AddScoped<IAssignmentAudienceResolver, AssignmentAudienceResolver>();
        services.AddScoped<IMyAssignmentService, MyAssignmentService>();
        services.AddScoped<IAssignmentDeadlineNoticeService, AssignmentDeadlineNoticeService>();
        services.AddHostedService<AssignmentDeadlineSweepService>();

        return services;
    }
}
