using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using NUnit.Framework;
using Sellevate.Learning.Infrastructure.Ai;

namespace Sellevate.Learning.Tests.Unit;

/// <summary>
/// Mirrors the ai-service contract: a rejected LLM call surfaces as a typed
/// <see cref="OpenAiException"/> the controllers can map to a status code, and the
/// provider's response body never travels back to the caller in an exception message.
/// </summary>
[TestFixture]
public class OpenAiProviderErrorTests
{
    private sealed class StubHandler(HttpStatusCode statusCode, string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });
    }

    private static OpenAiChatService CreateService(HttpStatusCode statusCode, string body)
    {
        var httpClientFactory = Substitute.For<IHttpClientFactory>();
        httpClientFactory.CreateClient("OpenAI").Returns(_ => new HttpClient(new StubHandler(statusCode, body)));

        var options = Options.Create(new OpenAiConfiguration { ApiKey = "test-key" });
        return new OpenAiChatService(httpClientFactory, options, NullLogger<OpenAiChatService>.Instance);
    }

    private static Task<ChatMessageResult> CallAsync(OpenAiChatService service) =>
        service.SendChatMessageAsync("system", []);

    [Test]
    public async Task InvalidRequest_ThrowsTypedRequestException_NotAnUnhandledFailure()
    {
        var service = CreateService(HttpStatusCode.BadRequest, """{"error":{"message":"Invalid schema"}}""");

        var act = async () => await CallAsync(service);

        var exception = (await act.Should().ThrowAsync<OpenAiRequestException>()).Which;
        exception.StatusCode.Should().Be(400);
        exception.Should().BeAssignableTo<OpenAiException>();
    }

    [Test]
    public async Task InvalidRequest_DoesNotLeakTheProviderBody()
    {
        var service = CreateService(HttpStatusCode.BadRequest, """{"error":{"message":"sk-secret-key-leak"}}""");

        var act = async () => await CallAsync(service);

        var exception = (await act.Should().ThrowAsync<OpenAiRequestException>()).Which;
        exception.Message.Should().Be("AI provider error");
        exception.Message.Should().NotContain("sk-secret");
    }

    [TestCase(HttpStatusCode.PaymentRequired, typeof(OpenAiPaymentRequiredException))]
    [TestCase(HttpStatusCode.TooManyRequests, typeof(OpenAiRateLimitException))]
    [TestCase(HttpStatusCode.Unauthorized, typeof(OpenAiAuthenticationException))]
    [TestCase(HttpStatusCode.Forbidden, typeof(OpenAiAuthenticationException))]
    [TestCase(HttpStatusCode.InternalServerError, typeof(OpenAiRequestException))]
    public async Task ProviderStatus_MapsOntoItsTypedException(HttpStatusCode statusCode, Type expectedException)
    {
        var service = CreateService(statusCode, """{"error":"nope"}""");

        var act = async () => await CallAsync(service);

        (await act.Should().ThrowAsync<Exception>()).Which.Should().BeOfType(expectedException);
    }

    [Test]
    public async Task NonJsonBodyOnSuccess_ThrowsTypedRequestException()
    {
        // A proxy that answers 200 with an HTML error page must not crash the request.
        var service = CreateService(HttpStatusCode.OK, "<html><body>502 Bad Gateway</body></html>");

        var act = async () => await CallAsync(service);

        await act.Should().ThrowAsync<OpenAiRequestException>();
    }

    [Test]
    public async Task EveryProviderFailure_IsCatchableAsTheSharedBaseType()
    {
        // Controllers catch OpenAiException; a new failure mode must never bypass them.
        var service = CreateService(HttpStatusCode.BadGateway, """{"error":"upstream"}""");

        var act = async () => await CallAsync(service);

        await act.Should().ThrowAsync<OpenAiException>();
    }
}
