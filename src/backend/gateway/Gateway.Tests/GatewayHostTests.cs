using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using NUnit.Framework;

namespace Sellevate.Gateway.Tests;

/// <summary>
/// Boots the gateway in-process. Proves the host wires up cleanly — JWT validation,
/// the YARP reverse-proxy config section, and the local health endpoint all load
/// without a live broker or downstream service.
/// </summary>
[TestFixture]
public class GatewayHostTests
{
    private WebApplicationFactory<Program> _factory = null!;

    [OneTimeSetUp]
    public void SetUp() => _factory = new WebApplicationFactory<Program>();

    [OneTimeTearDown]
    public void TearDown() => _factory.Dispose();

    [Test]
    public async Task Healthz_returns_ok_without_a_running_monolith()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/healthz");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<HealthResponse>();
        body!.Status.Should().Be("ok");
        body.Service.Should().Be("gateway");
    }

    private sealed record HealthResponse(string Status, string Service);
}
