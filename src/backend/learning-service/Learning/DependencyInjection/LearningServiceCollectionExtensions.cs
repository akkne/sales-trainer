using Sellevate.Learning.Eventing;
using Sellevate.Learning.Features.Exercises;
using Sellevate.Learning.Features.Reference;
using Sellevate.Learning.Features.SkillTree;
using Sellevate.Learning.Features.Techniques;
using Sellevate.Learning.Infrastructure.Ai;

namespace Sellevate.Learning.DependencyInjection;

public static class LearningServiceCollectionExtensions
{
    public static IServiceCollection AddLearningServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddScoped<ILearningEventPublisher, KafkaLearningEventPublisher>();

        services.AddAiEvaluationClient(configuration);

        services.AddSkillTreeFeatureServices();
        services.AddExerciseFeatureServices();
        services.AddExerciseDialogServices(configuration);
        services.AddReferenceFeatureServices();
        services.AddTechniqueFeatureServices();

        services.AddHostedService<UserReplicaConsumer>();

        return services;
    }
}
