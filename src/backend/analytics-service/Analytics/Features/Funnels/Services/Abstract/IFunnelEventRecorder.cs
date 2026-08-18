using Sellevate.BuildingBlocks.Eventing;

namespace Sellevate.Analytics.Features.Funnels.Services.Abstract;

/// <summary>
/// Folds a conversion-relevant integration event into the platform funnel counters.
/// </summary>
public interface IFunnelEventRecorder
{
    /// <summary>
    /// Returns <c>false</c> when the envelope carries nothing this recorder counts — an unrelated
    /// topic, an undeserializable payload, or a value outside a bounded label set. That is an
    /// "ignored", never a failure: implementations must not throw on unrecognised input, because the
    /// caller is a Kafka consumer that would dead-letter the message and silently stop the funnel.
    /// </summary>
    bool Record(EventEnvelope envelope);
}
