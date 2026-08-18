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
        services.AddScoped<IAssignmentDashboardService, AssignmentDashboardService>();
        services.AddScoped<IAssignmentThresholdEvaluator, AssignmentThresholdEvaluator>();
        services.AddScoped<IAssignmentAudienceResolver, AssignmentAudienceResolver>();
        services.AddScoped<IMyAssignmentService, MyAssignmentService>();
        services.AddScoped<IAssignmentDeadlineNoticeService, AssignmentDeadlineNoticeService>();
        services.AddScoped<IAssignmentRepeatIssueService, AssignmentRepeatIssueService>();
        services.AddHostedService<AssignmentDeadlineSweepService>();
        services.AddHostedService<AssignmentRepeatSweepService>();

        return services;
    }
}
