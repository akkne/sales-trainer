using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NUnit.Framework;
using Sellevate.BuildingBlocks.Tenancy;
using Sellevate.Learning.Infrastructure.Ai;
using Sellevate.Learning.Infrastructure.Configuration;

namespace Sellevate.Learning.Tests.Unit;

/// <summary>
/// Mirrors the ai-service contract: a rejected LLM call surfaces as a typed
/// <see cref="OpenAiException"/> the controllers can map to a status code, and the
/// provider's response body never travels back to the caller in an exception message.
///
/// <para>
/// Phase 40.33 moved the provider call itself into ai-service — learning-service no longer holds a
/// key, an HttpClient to OpenAI, or the translation of a provider status. What it holds now is the
/// translation of ai-service's <b>named</b> failure back into the same exception hierarchy, so
/// <c>ExerciseController</c> and <c>ExerciseDialogService</c> keep catching exactly what they caught
/// before. These tests moved with it: same properties, one hop further out.
/// </para>
/// </summary>
[TestFixture]
public class OpenAiProviderErrorTests
{
    private sealed class StubHandler(HttpStatusCode statusCode, string body, string mediaType = "application/json")
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(body, Encoding.UTF8, mediaType)
            });
    }

    private static AiChatClient CreateService(
        HttpStatusCode statusCode,
        string body,
        string mediaType = "application/json")
    {
        var httpClient = new HttpClient(new StubHandler(statusCode, body, mediaType));
        var options = Options.Create(new AiServiceConfiguration { BaseUrl = "http://ai:8080" });
        var tenantContext = new TenantContext();
        tenantContext.SetOrganization(Guid.NewGuid());

        return new AiChatClient(httpClient, options, tenantContext, NullLogger<AiChatClient>.Instance);
    }

    private static Task<ChatMessageResult> CallAsync(AiChatClient service) =>
        service.SendChatMessageAsync("system", []);

    [Test]
    public async Task InvalidRequest_ThrowsTypedRequestException_NotAnUnhandledFailure()
    {
        var service = CreateService(
            HttpStatusCode.ServiceUnavailable,
            """{"code":"provider_rejected","upstreamStatusCode":400}""");

        var act = async () => await CallAsync(service);

        var exception = (await act.Should().ThrowAsync<OpenAiRequestException>()).Which;
        exception.StatusCode.Should().Be(400);
        exception.Should().BeAssignableTo<OpenAiException>();
    }

    /// <summary>
    /// ai-service redacts and drops the provider body before it answers; the failure it names carries a code
    /// and a status and nothing a key could hide in. This asserts the far end of that promise — whatever
    /// arrives, the exception message stays the generic one.
    /// </summary>
    [Test]
    public async Task InvalidRequest_DoesNotLeakTheProviderBody()
    {
        var service = CreateService(
            HttpStatusCode.ServiceUnavailable,
            """{"code":"provider_rejected","upstreamStatusCode":400,"detail":"sk-secret-key-leak"}""");

        var act = async () => await CallAsync(service);

        var exception = (await act.Should().ThrowAsync<OpenAiRequestException>()).Which;
        exception.Message.Should().Be("AI provider error");
        exception.Message.Should().NotContain("sk-secret");
    }

    [TestCase("payment_required", HttpStatusCode.PaymentRequired, typeof(OpenAiPaymentRequiredException))]
    [TestCase("rate_limited", HttpStatusCode.TooManyRequests, typeof(OpenAiRateLimitException))]
    [TestCase("provider_auth", HttpStatusCode.ServiceUnavailable, typeof(OpenAiAuthenticationException))]
    [TestCase("provider_failed", HttpStatusCode.ServiceUnavailable, typeof(OpenAiRequestException))]
    [TestCase("provider_unreachable", HttpStatusCode.ServiceUnavailable, typeof(OpenAiRequestException))]
    public async Task ProviderFailureCode_MapsOntoItsTypedException(
        string failureCode,
        HttpStatusCode statusCode,
        Type expectedException)
    {
        var service = CreateService(statusCode, $$"""{"code":"{{failureCode}}"}""");

        var act = async () => await CallAsync(service);

        (await act.Should().ThrowAsync<Exception>()).Which.Should().BeOfType(expectedException);
    }

    /// <summary>
    /// Phase 40.33. The organization spent its allowance. It is not a provider failure and it has no failure
    /// code, but the exercise chat must degrade the same way rather than 500 — so it arrives as the
    /// rate-limit exception, which <c>GenerateAiResponseAsync</c> already answers with a neutral reply
    /// instead of a broken screen.
    /// </summary>
    [Test]
    public async Task OrganizationQuotaRefusal_DegradesAsARateLimit()
    {
        var service = CreateService(
            HttpStatusCode.TooManyRequests,
            """{"error":"Organization AI quota reached","resource":"llm_tokens"}""");

        var act = async () => await CallAsync(service);

        await act.Should().ThrowAsync<OpenAiRateLimitException>();
    }

    /// <summary>
    /// A proxy that answers 200 with an HTML error page must not crash the request.
    /// </summary>
    [Test]
    public async Task NonJsonBodyOnSuccess_ThrowsTypedRequestException()
    {
        var service = CreateService(HttpStatusCode.OK, "<html><body>502 Bad Gateway</body></html>", "text/html");

        var act = async () => await CallAsync(service);

        await act.Should().ThrowAsync<OpenAiRequestException>();
    }

    /// <summary>
    /// Controllers catch <see cref="OpenAiException"/>; a new failure mode must never bypass them —
    /// including one ai-service adds later and this client has never heard of.
    /// </summary>
    [Test]
    public async Task EveryProviderFailure_IsCatchableAsTheSharedBaseType()
    {
        var service = CreateService(HttpStatusCode.BadGateway, """{"code":"something_new"}""");

        var act = async () => await CallAsync(service);

        await act.Should().ThrowAsync<OpenAiException>();
    }
}
