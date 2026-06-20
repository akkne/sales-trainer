using Microsoft.Extensions.Options;
using Sellevate.BuildingBlocks.Eventing;
using Sellevate.BuildingBlocks.Idempotency;
using Sellevate.BuildingBlocks.Messaging;
using Sellevate.Gamification.Features.Gamification.Services.Abstract;

namespace Sellevate.Gamification.Eventing;

internal sealed class LearningEventsConsumer : KafkaConsumerBackgroundService
{
    public LearningEventsConsumer(
        IOptions<KafkaSettings> settings,
        IServiceScopeFactory scopeFactory,
        IIdempotencyStore idempotencyStore,
        ILogger<LearningEventsConsumer> logger)
        : base(settings, scopeFactory, idempotencyStore, logger)
    {
    }

    protected override IReadOnlyCollection<string> Topics =>
    [
        BuildingBlocks.Eventing.Topics.ExerciseCompleted,
        BuildingBlocks.Eventing.Topics.LessonCompleted,
        BuildingBlocks.Eventing.Topics.SkillCompleted,
    ];

    protected override async Task HandleAsync(EventEnvelope envelope, IServiceProvider scopedServices, CancellationToken cancellationToken)
    {
        var eventHandler = scopedServices.GetRequiredService<IGamificationEventHandler>();

        switch (envelope.Type)
        {
            case BuildingBlocks.Eventing.Topics.ExerciseCompleted:
            {
                var payload = envelope.DataAs<ExerciseCompletedEvent>();
                if (payload is null)
                {
                    return;
                }

                await eventHandler.HandleExerciseCompletedAsync(
                    payload.UserId, payload.ExerciseType, payload.IsCorrect, cancellationToken);
                break;
            }

            case BuildingBlocks.Eventing.Topics.LessonCompleted:
            {
                var payload = envelope.DataAs<LessonCompletedEvent>();
                if (payload is null)
                {
                    return;
                }

                await eventHandler.HandleLessonCompletedAsync(payload.UserId, cancellationToken);
                break;
            }

            case BuildingBlocks.Eventing.Topics.SkillCompleted:
            {
                var payload = envelope.DataAs<SkillCompletedEvent>();
                if (payload is null)
                {
                    return;
                }

                await eventHandler.HandleSkillCompletedAsync(payload.UserId, cancellationToken);
                break;
            }
        }
    }
}
