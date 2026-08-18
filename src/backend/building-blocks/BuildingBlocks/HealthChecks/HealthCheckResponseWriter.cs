using System.Net.Mime;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Sellevate.BuildingBlocks.HealthChecks;

/// <summary>
/// Writes a uniform JSON health-check payload (<c>{ status, checks: [{ name, status }] }</c>)
/// so every service's <c>/healthz</c> and <c>/readyz</c> respond in the same shape, which a
/// dashboard or uptime probe can parse identically across services.
/// </summary>
public static class HealthCheckResponseWriter
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Serializes <paramref name="report"/> onto the response. Deliberately does not set a status
    /// code: the health-check middleware has already chosen one from the aggregate status, and
    /// overwriting it here would decouple the body from the code a probe reads.
    /// </summary>
    public static Task WriteAsync(HttpContext httpContext, HealthReport report)
    {
        httpContext.Response.ContentType = MediaTypeNames.Application.Json;

        var payload = new HealthReportResponse(
            report.Status.ToString(),
            report.Entries
                .Select(entry => new HealthCheckEntryResponse(entry.Key, entry.Value.Status.ToString()))
                .ToArray());

        return httpContext.Response.WriteAsync(JsonSerializer.Serialize(payload, SerializerOptions));
    }

    private sealed record HealthReportResponse(string Status, IReadOnlyCollection<HealthCheckEntryResponse> Checks);

    private sealed record HealthCheckEntryResponse(string Name, string Status);
}
