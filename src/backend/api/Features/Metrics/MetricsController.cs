using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalesTrainer.Api.Features.Metrics.Constants;
using SalesTrainer.Api.Features.Metrics.Models;
using SalesTrainer.Api.Infrastructure.Metrics;

namespace SalesTrainer.Api.Features.Metrics;

/// <summary>
/// Receives lightweight usage events from the frontend and folds them into Prometheus
/// counters. Lives under <c>/tracking</c> because <c>/metrics</c> is owned by the
/// prometheus-net scrape endpoint. Event/page names are validated against a server-side
/// whitelist so client input can never explode label cardinality.
/// </summary>
[ApiController]
[Route("tracking")]
[Authorize]
public sealed class MetricsController : ControllerBase
{
    [HttpPost("events")]
    public IActionResult TrackEvent([FromBody] TrackEventRequestDto request)
    {
        if (!TrackedEvents.IsKnownEvent(request.Event) || !TrackedEvents.IsKnownPage(request.Page))
            return BadRequest(new { message = "Unknown event or page." });

        if (request.Event == TrackedEvents.PageViewEvent)
            AppMetrics.PageViews.WithLabels(request.Page).Inc();
        else
            AppMetrics.Events.WithLabels(request.Event, request.Page).Inc();

        return NoContent();
    }
}
