using System.Net;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using NUnit.Framework;
using SalesTrainer.Api.Features.Voice.Models;
using SalesTrainer.Api.Features.Voice.Services.Implementation;
using SalesTrainer.Api.Infrastructure.Configuration;

namespace SalesTrainer.Tests.Unit;

[TestFixture]
public class VoicerTtsServiceTests
{
    private static readonly byte[] FakeMp3 = [0xFF, 0xFB, 0x90, 0x00, 0x01, 0x02];

    private static VoicerTtsService CreateService(
        HttpMessageHandler handler,
        string? apiKey = "real-key")
    {
        var options = Options.Create(new VoicerTtsConfiguration
        {
            ApiKey = apiKey!,
            BaseUrl = "https://voicer.test",
            PollIntervalMilliseconds = 1,
            MaximumPollAttemptCount = 5,
        });

        var httpClientFactory = Substitute.For<IHttpClientFactory>();
        httpClientFactory.CreateClient("VoicerTts").Returns(new HttpClient(handler));

        return new VoicerTtsService(httpClientFactory, options, NullLogger<VoicerTtsService>.Instance);
    }

    [Test]
    public void IsConfigured_FalseForMissingOrPlaceholderKey()
    {
        CreateService(new SequenceHandler(), apiKey: null).IsConfigured.Should().BeFalse();
        CreateService(new SequenceHandler(), apiKey: "  ").IsConfigured.Should().BeFalse();
        CreateService(new SequenceHandler(), apiKey: "REPLACE_WITH_VOICER_API_KEY").IsConfigured.Should().BeFalse();
        CreateService(new SequenceHandler(), apiKey: "real-key").IsConfigured.Should().BeTrue();
    }

    [Test]
    public async Task SynthesizeSpeechAsync_NotConfigured_Throws()
    {
        var service = CreateService(new SequenceHandler(), apiKey: null);

        var act = () => service.SynthesizeSpeechAsync("Привет");

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Test]
    public async Task SynthesizeSpeechAsync_HappyPath_ReturnsAudioStream()
    {
        var handler = new SequenceHandler(
            Json(HttpStatusCode.OK, """{"task_id": 42}"""),
            Json(HttpStatusCode.OK, """{"status": "processing"}"""),
            Json(HttpStatusCode.OK, """{"status": "ending"}"""),
            Binary(HttpStatusCode.OK, FakeMp3));

        var service = CreateService(handler);

        await using var stream = await service.SynthesizeSpeechAsync("Добрый день!");

        using var memory = new MemoryStream();
        await stream.CopyToAsync(memory);
        memory.ToArray().Should().Equal(FakeMp3);

        handler.Requests.Should().HaveCount(4);
        handler.Requests[0].Should().EndWith("/tasks");
        handler.Requests[1].Should().EndWith("/tasks/42/status");
        handler.Requests[3].Should().EndWith("/tasks/42/result");
    }

    [Test]
    public async Task SynthesizeSpeechAsync_Unauthorized_ThrowsAuthenticationException()
    {
        var handler = new SequenceHandler(Json(HttpStatusCode.Unauthorized, """{"detail": "bad key"}"""));
        var service = CreateService(handler);

        var act = () => service.SynthesizeSpeechAsync("Привет");

        await act.Should().ThrowAsync<VoicerTtsAuthenticationException>();
    }

    [Test]
    public async Task SynthesizeSpeechAsync_PaymentRequired_ThrowsInsufficientFunds()
    {
        var handler = new SequenceHandler(Json(HttpStatusCode.PaymentRequired, """{"detail": "no funds"}"""));
        var service = CreateService(handler);

        var act = () => service.SynthesizeSpeechAsync("Привет");

        await act.Should().ThrowAsync<VoicerTtsInsufficientFundsException>();
    }

    [Test]
    public async Task SynthesizeSpeechAsync_RateLimited_ThrowsRateLimitException()
    {
        var handler = new SequenceHandler(Json((HttpStatusCode)429, """{"detail": "too many"}"""));
        var service = CreateService(handler);

        var act = () => service.SynthesizeSpeechAsync("Привет");

        await act.Should().ThrowAsync<VoicerTtsRateLimitException>();
    }

    [Test]
    public async Task SynthesizeSpeechAsync_TaskErrorStatus_ThrowsVoicerTtsException()
    {
        var handler = new SequenceHandler(
            Json(HttpStatusCode.OK, """{"task_id": 7}"""),
            Json(HttpStatusCode.OK, """{"status": "error", "status_label": "synthesis failed"}"""));
        var service = CreateService(handler);

        var act = () => service.SynthesizeSpeechAsync("Привет");

        await act.Should().ThrowAsync<VoicerTtsException>()
            .Where(e => e.Message.Contains("synthesis failed"));
    }

    [Test]
    public async Task SynthesizeSpeechAsync_NeverCompletes_ThrowsTimeout()
    {
        // 1 create + 5 polls (MaxPollAttempts) — all stuck in processing
        var responses = new List<Func<HttpResponseMessage>> { Json(HttpStatusCode.OK, """{"task_id": 9}""") };
        responses.AddRange(Enumerable.Range(0, 5).Select(_ => Json(HttpStatusCode.OK, """{"status": "processing"}""")));

        var service = CreateService(new SequenceHandler(responses.ToArray()));

        var act = () => service.SynthesizeSpeechAsync("Привет");

        await act.Should().ThrowAsync<VoicerTtsTimeoutException>();
    }

    private static Func<HttpResponseMessage> Json(HttpStatusCode status, string body) =>
        () => new HttpResponseMessage(status) { Content = new StringContent(body) };

    private static Func<HttpResponseMessage> Binary(HttpStatusCode status, byte[] body) =>
        () => new HttpResponseMessage(status) { Content = new ByteArrayContent(body) };

    /// <summary>Returns queued responses in order; records request URLs.</summary>
    private sealed class SequenceHandler : HttpMessageHandler
    {
        private readonly Queue<Func<HttpResponseMessage>> _responses;
        public List<string> Requests { get; } = [];

        public SequenceHandler(params Func<HttpResponseMessage>[] responses)
        {
            _responses = new Queue<Func<HttpResponseMessage>>(responses);
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request.RequestUri!.AbsolutePath);
            if (_responses.Count == 0)
                throw new InvalidOperationException("No more queued responses");
            return Task.FromResult(_responses.Dequeue()());
        }
    }
}
