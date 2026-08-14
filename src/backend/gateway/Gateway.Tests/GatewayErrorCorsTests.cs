using System.Net.Http;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using NUnit.Framework;

namespace Sellevate.Gateway.Tests;

/// <summary>
/// A response the gateway writes itself (unknown route, upstream timeout, upstream unreachable)
/// carries no downstream CORS headers. Without them the browser reports the failure as
/// "blocked by CORS policy" and the status code that explains it never reaches the client.
/// </summary>
[TestFixture]
public class GatewayErrorCorsTests
{
    private WebApplicationFactory<Program> _factory = null!;

    [OneTimeSetUp]
    public void SetUp() => _factory = new WebApplicationFactory<Program>();

    [OneTimeTearDown]
    public void TearDown() => _factory.Dispose();

    [Test]
    public async Task Gateway_generated_response_allows_the_frontend_origin()
    {
        var client = _factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Get, "/this/route/does/not/exist");
        request.Headers.Add("Origin", "http://localhost:3000");

        var response = await client.SendAsync(request);

        response.Headers.GetValues("Access-Control-Allow-Origin")
            .Should().ContainSingle().Which.Should().Be("http://localhost:3000");
        response.Headers.GetValues("Access-Control-Allow-Credentials")
            .Should().ContainSingle().Which.Should().Be("true");
    }

    [Test]
    public async Task An_unknown_origin_is_not_allowed()
    {
        var client = _factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Get, "/this/route/does/not/exist");
        request.Headers.Add("Origin", "http://evil.example.com");

        var response = await client.SendAsync(request);

        response.Headers.Contains("Access-Control-Allow-Origin").Should().BeFalse();
    }

    [Test]
    public async Task A_request_without_an_origin_gets_no_cors_headers()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/this/route/does/not/exist");

        response.Headers.Contains("Access-Control-Allow-Origin").Should().BeFalse();
    }
}
