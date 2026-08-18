using Sellevate.Learning.Features.Assignments.Services.Abstract;
using Sellevate.Learning.Features.Assignments.Services.Implementation;

namespace Sellevate.Learning.Features.Assignments;

/// <summary>
/// Registers the assignment feature: the write and read services, the threshold evaluator, and the two
/// background sweeps.
///
/// <para>
/// Everything here is <c>Scoped</c> except the two sweeps, which are hosted services that open a scope
/// per organization themselves. Nothing assignment-related may become a <c>Singleton</c>: every service
/// in this list closes over a tenant-scoped <c>LearningDbContext</c>, and a singleton capturing one
/// would serve the first organization's query filter to every later caller (CODESTYLE §4).
/// </para>
/// </summary>
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
