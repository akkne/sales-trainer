using Sellevate.Learning.Features.SkillTree.Services.Abstract;
using Sellevate.Learning.Features.SkillTree.Services.Implementation;

namespace Sellevate.Learning.Features.SkillTree;

public static class SkillTreeServiceCollectionExtensions
{
    public static IServiceCollection AddSkillTreeFeatureServices(this IServiceCollection services)
    {
        services.AddScoped<ISkillTreeService, SkillTreeService>();
        return services;
    }
}
