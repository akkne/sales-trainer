using Sellevate.BuildingBlocks.Outbox;
using Sellevate.Learning.Eventing;
using Sellevate.Learning.Features.Assignments;
using Sellevate.Learning.Features.Content;
using Sellevate.Learning.Features.Exercises;
using Sellevate.Learning.Features.Lessons;
using Sellevate.Learning.Features.Programs;
using Sellevate.Learning.Features.Reference;
using Sellevate.Learning.Features.SkillTree;
using Sellevate.Learning.Features.Techniques;
using Sellevate.Learning.Infrastructure.Ai;
using Sellevate.Learning.Infrastructure.Identity;

namespace Sellevate.Learning.DependencyInjection;

public static class LearningServiceCollectionExtensions
{
    public static IServiceCollection AddLearningServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddScoped<ILearningEventPublisher, KafkaLearningEventPublisher>();
        services.AddScoped<IOutboxWriter, LearningOutboxWriter>();
        services.AddScoped<IOutboxStore, LearningOutboxStore>();
        services.AddHostedService<OutboxRelayBackgroundService>();

        services.AddAiEvaluationClient(configuration);
        services.AddIdentityDirectoryClient(configuration);

        services.AddContentOverrideServices();
        services.AddSkillTreeFeatureServices();
        services.AddLessonFeatureServices();
        services.AddProgramFeatureServices();
        services.AddAssignmentFeatureServices(configuration);
        services.AddExerciseFeatureServices();
        services.AddExerciseDialogServices(configuration);
        services.AddReferenceFeatureServices();
        services.AddTechniqueFeatureServices();

        services.AddHostedService<UserReplicaConsumer>();
        services.AddHostedService<OrganizationProfileConsumer>();
        services.AddHostedService<AssignmentThresholdConsumer>();

        return services;
    }
}
