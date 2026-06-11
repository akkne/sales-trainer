using SalesTrainer.Api.Features.SkillTree.Services.Abstract;
using SalesTrainer.Api.Features.SkillTree.Services.Implementation;
using SalesTrainer.Api.Infrastructure.Configuration;

namespace SalesTrainer.Api.Features.SkillTree;

public static class SkillTreeServiceCollectionExtensions
{
    public static IServiceCollection AddSkillTreeFeatureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<GamificationConfiguration>(
            configuration.GetSection(GamificationConfiguration.SectionName));
        services.AddScoped<ISkillTreeService, SkillTreeService>();
        return services;
    }
}
