using Sellevate.Analytics.Features.Funnels.Models;
using Sellevate.Analytics.Features.Funnels.Services.Abstract;
using Sellevate.Analytics.Infrastructure.Metrics;
using Sellevate.BuildingBlocks.Eventing;

namespace Sellevate.Analytics.Features.Funnels.Services.Implementation;

internal sealed class FunnelEventRecorder : IFunnelEventRecorder
{
    public bool Record(EventEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        switch (envelope.Type)
        {
            case Topics.UserRegistered:
            {
                var payload = envelope.DataAs<UserRegisteredEvent>();
                if (payload is null)
                {
                    return false;
                }

                AppMetrics.Registrations.Inc();
                return true;
            }

            case Topics.ExerciseCompleted:
            {
                var payload = envelope.DataAs<ExerciseCompletedEvent>();
                if (payload is null)
                {
                    return false;
                }

                AppMetrics.ExercisesCompleted.Inc();
                return true;
            }

            case Topics.XpGranted:
            {
                var payload = envelope.DataAs<ExperiencePointsGrantedEvent>();
                if (payload is null)
                {
                    return false;
                }

                // Guard: negative or zero amounts would throw in Prometheus Counter.Inc() and
                // send the message to the DLQ. Treat them as ignored (not poison).
                if (payload.Amount <= 0)
                {
                    return false;
                }

                AppMetrics.ExperiencePointsGranted.Inc(payload.Amount);
                return true;
            }

            default:
                return false;
        }
    }
}
