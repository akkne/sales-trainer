using Sellevate.Learning.Features.ContentAdaptation.Services.Abstract;
using Sellevate.Learning.Features.ContentAdaptation.Services.Implementation;

namespace Sellevate.Learning.Features.ContentAdaptation;

/// <summary>Phase 40.32. Batch tone adaptation and AI content review: request half, worker half, clock.</summary>
public static class ContentAdaptationServiceCollectionExtensions
{
    public static IServiceCollection AddContentAdaptationFeatureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<ContentAdaptationOptions>(
            configuration.GetSection(ContentAdaptationOptions.SectionName));

        services.AddScoped<IContentAdaptationJobService, ContentAdaptationJobService>();
        services.AddScoped<IContentAdaptationStepRunner, ContentAdaptationStepRunner>();
        services.AddHostedService<ContentAdaptationSweepService>();

        return services;
    }
}
