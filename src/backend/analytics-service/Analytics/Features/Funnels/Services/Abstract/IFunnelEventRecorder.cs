using Sellevate.BuildingBlocks.Eventing;

namespace Sellevate.Analytics.Features.Funnels.Services.Abstract;

public interface IFunnelEventRecorder
{
    bool Record(EventEnvelope envelope);
}
