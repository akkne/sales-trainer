using Sellevate.Learning.Features.ContentGeneration.Services.Abstract;
using Sellevate.Learning.Features.ContentGeneration.Services.Implementation;

namespace Sellevate.Learning.Features.ContentGeneration;

/// <summary>Phase 40.27. The admin content pipeline: its request half, its worker half and the clock.</summary>
public static class ContentGenerationServiceCollectionExtensions
{
    public static IServiceCollection AddContentGenerationFeatureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<ContentGenerationOptions>(
            configuration.GetSection(ContentGenerationOptions.SectionName));

        services.AddScoped<IContentGenerationJobService, ContentGenerationJobService>();
        services.AddScoped<IContentGenerationStepRunner, ContentGenerationStepRunner>();
        services.AddHostedService<ContentGenerationSweepService>();

        return services;
    }
}
