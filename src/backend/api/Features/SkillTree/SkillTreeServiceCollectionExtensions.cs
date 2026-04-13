namespace SalesTrainer.Api.Features.SkillTree;

public static class SkillTreeServiceCollectionExtensions
{
    public static IServiceCollection AddSkillTreeFeatureServices(this IServiceCollection services)
    {
        services.AddScoped<ISkillTreeService, SkillTreeService>();
        return services;
    }
}
