using Sellevate.Analytics.Features.Funnels.Models;
using Sellevate.Analytics.Features.Funnels.Services.Abstract;
using Sellevate.Analytics.Infrastructure.Metrics;
using Sellevate.BuildingBlocks.Eventing;

namespace Sellevate.Analytics.Features.Funnels.Services.Implementation;

internal sealed class FunnelEventRecorder : IFunnelEventRecorder
{
    /// <summary>
    /// Phase 40.25. The four values of learning-service's <c>AssignmentProgressStatuses</c>, copied
    /// rather than referenced: analytics-service does not depend on learning-service and must not
    /// start to for a list of four strings. Copied lists drift, so an unrecognised value is dropped
    /// loudly-in-the-logs rather than counted — see the guard at the call site.
    /// </summary>
    private static readonly HashSet<string> KnownProgressStates =
        new(StringComparer.Ordinal) { "not_started", "in_progress", "completed", "failed_threshold" };

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

            case Topics.AssignmentIssued:
            {
                var payload = envelope.DataAs<AssignmentIssuedEvent>();
                if (payload is null)
                {
                    return false;
                }

                AppMetrics.AssignmentsIssued.Inc();
                return true;
            }

            case Topics.AssignmentProgressChanged:
            {
                var payload = envelope.DataAs<AssignmentProgressChangedEvent>();
                if (payload is null || string.IsNullOrWhiteSpace(payload.Status))
                {
                    return false;
                }

                // Guard: the label set has to stay bounded. A producer that grows a fifth status
                // without this service knowing would otherwise add a Prometheus time series per
                // unknown value — the cardinality failure this file avoids everywhere else by
                // refusing an organization label. An unknown status is ignored, not counted under a
                // catch-all, because a bucket named "other" is a number nobody can act on.
                if (!KnownProgressStates.Contains(payload.Status))
                {
                    return false;
                }

                AppMetrics.AssignmentProgressTransitions.WithLabels(payload.Status).Inc();
                return true;
            }

            default:
                return false;
        }
    }
}
