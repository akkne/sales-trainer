using Microsoft.Extensions.Options;
using Sellevate.BuildingBlocks.Eventing;
using Sellevate.BuildingBlocks.Idempotency;
using Sellevate.BuildingBlocks.Messaging;
using Sellevate.Gamification.Features.Gamification.Services.Abstract;

namespace Sellevate.Gamification.Eventing;

/// <summary>
/// Consumes learning-service's completion events and hands each to the gamification fan-out.
///
/// <para>
/// Requires an organization on the envelope (inherited default): every consequence writes tenant-scoped
/// tables, so an envelope without one is retried and dead-lettered rather than guessed at. The envelope's
/// event id is forwarded as the grant's idempotency key, which is what makes at-least-once redelivery
/// safe. An envelope whose payload does not deserialize is dropped, not retried — a malformed message
/// will not become well-formed on the next attempt.
/// </para>
/// </summary>
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
                    payload.UserId, payload.ExerciseType, payload.IsCorrect, envelope.EventId, cancellationToken);
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
