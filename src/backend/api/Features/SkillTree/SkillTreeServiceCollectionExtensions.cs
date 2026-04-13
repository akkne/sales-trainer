using SalesTrainer.Api.Features.SkillTree.Services.Abstract;
using SalesTrainer.Api.Features.SkillTree.Services.Implementation;

namespace SalesTrainer.Api.Features.SkillTree;

public static class SkillTreeServiceCollectionExtensions
{
    public static IServiceCollection AddSkillTreeFeatureServices(this IServiceCollection services)
    {
        services.AddScoped<ISkillTreeService, SkillTreeService>();
        return services;
    }
}
