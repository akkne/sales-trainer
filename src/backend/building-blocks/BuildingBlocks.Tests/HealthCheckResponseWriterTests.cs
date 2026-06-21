using System.Collections.Generic;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using NUnit.Framework;
using Sellevate.BuildingBlocks.HealthChecks;

namespace Sellevate.BuildingBlocks.Tests;

[TestFixture]
public sealed class HealthCheckResponseWriterTests
{
    [Test]
    public async Task WriteAsync_SerializesAggregateStatusAndPerCheckEntries()
    {
        var entries = new Dictionary<string, HealthReportEntry>
        {
            [HealthCheckConstants.RedisCheckName] = new HealthReportEntry(
                HealthStatus.Healthy, description: null, duration: TimeSpan.Zero, exception: null, data: null),
            [HealthCheckConstants.KafkaCheckName] = new HealthReportEntry(
                HealthStatus.Unhealthy, description: null, duration: TimeSpan.Zero, exception: null, data: null),
        };
        var report = new HealthReport(entries, HealthStatus.Unhealthy, TimeSpan.Zero);

        var httpContext = new DefaultHttpContext();
        using var responseBody = new MemoryStream();
        httpContext.Response.Body = responseBody;

        await HealthCheckResponseWriter.WriteAsync(httpContext, report);

        httpContext.Response.ContentType.Should().Be("application/json");
        responseBody.Position = 0;
        using var document = JsonDocument.Parse(responseBody);
        var root = document.RootElement;

        root.GetProperty("status").GetString().Should().Be("Unhealthy");
        var checks = root.GetProperty("checks");
        checks.GetArrayLength().Should().Be(2);
        checks[0].GetProperty("name").GetString().Should().Be(HealthCheckConstants.RedisCheckName);
        checks[0].GetProperty("status").GetString().Should().Be("Healthy");
        checks[1].GetProperty("name").GetString().Should().Be(HealthCheckConstants.KafkaCheckName);
        checks[1].GetProperty("status").GetString().Should().Be("Unhealthy");
    }

    [Test]
    public async Task WriteAsync_WithNoChecks_ReportsHealthyAndEmptyChecks()
    {
        var report = new HealthReport(
            new Dictionary<string, HealthReportEntry>(), HealthStatus.Healthy, TimeSpan.Zero);

        var httpContext = new DefaultHttpContext();
        using var responseBody = new MemoryStream();
        httpContext.Response.Body = responseBody;

        await HealthCheckResponseWriter.WriteAsync(httpContext, report);

        responseBody.Position = 0;
        using var document = JsonDocument.Parse(responseBody);
        var root = document.RootElement;

        root.GetProperty("status").GetString().Should().Be("Healthy");
        root.GetProperty("checks").GetArrayLength().Should().Be(0);
    }
}
