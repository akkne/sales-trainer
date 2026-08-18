using Sellevate.Analytics.Features.Funnels.Constants;
using Sellevate.Analytics.Features.Funnels.Models;
using Sellevate.Analytics.Features.Funnels.Services.Abstract;
using Sellevate.Analytics.Infrastructure.Metrics;
using Sellevate.BuildingBlocks.Eventing;

namespace Sellevate.Analytics.Features.Funnels.Services.Implementation;

/// <summary>
/// Turns one integration event into one Prometheus counter increment, and nothing else — no store,
/// no outgoing call, no per-tenant state.
///
/// <para>
/// Every rejection path returns <c>false</c> instead of throwing, and that is the invariant a caller
/// must respect: an event this recorder cannot make sense of is <b>not</b> poison. A payload that
/// fails to deserialize, a non-positive experience-point amount (which would throw inside
/// <c>Counter.Inc()</c>), and an assignment status outside the compiled-in set are all ignored, so a
/// producer change can flatten a graph but can never dead-letter a message or stall the consumer.
/// </para>
///
/// <para>
/// An unrecognised value is dropped rather than counted under a catch-all bucket. Folding it into a
/// known state would corrupt a number people act on; naming it "other" would create the unbounded
/// label cardinality this whole subsystem is shaped to avoid. Dropping it is the only option that
/// leaves the remaining numbers verifiable — see docs/ANALYTICS_SERVICE.md.
/// </para>
/// </summary>
internal sealed class FunnelEventRecorder : IFunnelEventRecorder
{
    public bool Record(EventEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        switch (envelope.Type)
        {
            case Topics.UserRegistered:
            {
                if (envelope.DataAs<UserRegisteredEvent>() is null)
                {
                    return false;
                }

                AppMetrics.Registrations.Inc();
                return true;
            }

            case Topics.ExerciseCompleted:
            {
                if (envelope.DataAs<ExerciseCompletedEvent>() is null)
                {
                    return false;
                }

                AppMetrics.ExercisesCompleted.Inc();
                return true;
            }

            case Topics.XpGranted:
            {
                var payload = envelope.DataAs<ExperiencePointsGrantedEvent>();
                if (payload is null || payload.Amount <= 0)
                {
                    return false;
                }

                AppMetrics.ExperiencePointsGranted.Inc(payload.Amount);
                return true;
            }

            case Topics.AssignmentIssued:
            {
                if (envelope.DataAs<AssignmentIssuedEvent>() is null)
                {
                    return false;
                }

                AppMetrics.AssignmentsIssued.Inc();
                return true;
            }

            case Topics.AssignmentProgressChanged:
            {
                var payload = envelope.DataAs<AssignmentProgressChangedEvent>();
                if (payload is null
                    || string.IsNullOrWhiteSpace(payload.Status)
                    || !AssignmentProgressStatuses.Known.Contains(payload.Status))
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
