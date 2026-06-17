namespace SalesTrainer.Api.Features.Metrics.Models;

/// <summary>
/// A single usage event from the frontend. <c>Event</c> = "page_view" is treated as a
/// page view; both fields are whitelist-validated server-side.
/// </summary>
public sealed record TrackEventRequestDto(string Event, string Page);
