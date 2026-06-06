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
public class YandexTtsServiceTests
{
    private static readonly byte[] FakePcm = [0x01, 0x02, 0x03, 0x04, 0x05, 0x06];

    private static YandexTtsService CreateService(
        RecordingHandler handler,
        string? apiKey = "real-key")
    {
        var options = Options.Create(new YandexTtsConfiguration
        {
            ApiKey = apiKey!,
            BaseUrl = "https://yandex.test",
            Voice = "marina",
            Lang = "ru-RU",
            Speed = "1.0",
        });

        var httpClientFactory = Substitute.For<IHttpClientFactory>();
        httpClientFactory.CreateClient("YandexTts").Returns(new HttpClient(handler));

        return new YandexTtsService(httpClientFactory, options, NullLogger<YandexTtsService>.Instance);
    }

    [Test]
    public void IsConfigured_FalseForMissingOrPlaceholderKey()
    {
        CreateService(new RecordingHandler(), apiKey: null).IsConfigured.Should().BeFalse();
        CreateService(new RecordingHandler(), apiKey: "  ").IsConfigured.Should().BeFalse();
        CreateService(new RecordingHandler(), apiKey: "REPLACE_WITH_YANDEX_API_KEY").IsConfigured.Should().BeFalse();
        CreateService(new RecordingHandler(), apiKey: "real-key").IsConfigured.Should().BeTrue();
    }

    [Test]
    public async Task SynthesizeSpeechAsync_NotConfigured_Throws()
    {
        var service = CreateService(new RecordingHandler(), apiKey: null);

        var act = () => service.SynthesizeSpeechAsync("Привет");

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Test]
    public async Task SynthesizeSpeechAsync_HappyPath_ReturnsWavWrappedAudio()
    {
        var handler = new RecordingHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(FakePcm),
        });
        var service = CreateService(handler);

        await using var stream = await service.SynthesizeSpeechAsync("Добрый день!");

        using var memory = new MemoryStream();
        await stream.CopyToAsync(memory);
        var wav = memory.ToArray();

        // 44-byte WAV header wrapping the raw PCM returned by the API.
        wav.Should().HaveCount(44 + FakePcm.Length);
        wav[..4].Should().Equal("RIFF"u8.ToArray());
        wav[8..12].Should().Equal("WAVE"u8.ToArray());
        wav[44..].Should().Equal(FakePcm);
        // 48 kHz default sample rate at offset 24 (little-endian).
        BitConverter.ToInt32(wav, 24).Should().Be(48000);

        handler.Requests.Should().ContainSingle()
            .Which.Path.Should().Be("/speech/v1/tts:synthesize");
        handler.Requests[0].AuthorizationHeader.Should().Be("Api-Key real-key");
        handler.Requests[0].FormBody.Should().Contain("voice=marina");
        handler.Requests[0].FormBody.Should().Contain("format=lpcm");
        handler.Requests[0].FormBody.Should().Contain("sampleRateHertz=48000");
        handler.Requests[0].FormBody.Should().Contain("lang=ru-RU");
    }

    [Test]
    public async Task SynthesizeSpeechAsync_ExplicitVoice_OverridesConfiguredOne()
    {
        var handler = new RecordingHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(FakePcm),
        });
        var service = CreateService(handler);

        await using var _ = await service.SynthesizeSpeechAsync("Привет", voice: "filipp");

        handler.Requests[0].FormBody.Should().Contain("voice=filipp");
    }

    [Test]
    public async Task SynthesizeSpeechAsync_Unauthorized_ThrowsAuthenticationException()
    {
        var handler = new RecordingHandler(new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {
            Content = new StringContent("""{"message": "invalid key"}"""),
        });
        var service = CreateService(handler);

        var act = () => service.SynthesizeSpeechAsync("Привет");

        await act.Should().ThrowAsync<YandexTtsAuthenticationException>();
    }

    [Test]
    public async Task SynthesizeSpeechAsync_RateLimited_ThrowsRateLimitException()
    {
        var handler = new RecordingHandler(new HttpResponseMessage((HttpStatusCode)429)
        {
            Content = new StringContent("""{"message": "too many requests"}"""),
        });
        var service = CreateService(handler);

        var act = () => service.SynthesizeSpeechAsync("Привет");

        await act.Should().ThrowAsync<YandexTtsRateLimitException>();
    }

    [Test]
    public async Task SynthesizeSpeechAsync_ServerError_ThrowsYandexTtsException()
    {
        var handler = new RecordingHandler(new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = new StringContent("""{"message": "boom"}"""),
        });
        var service = CreateService(handler);

        var act = () => service.SynthesizeSpeechAsync("Привет");

        await act.Should().ThrowAsync<YandexTtsException>()
            .Where(e => e.Message.Contains("boom"));
    }

    public sealed record RecordedRequest(string Path, string? AuthorizationHeader, string FormBody);

    /// <summary>Returns the queued response; records path, auth header and form body.</summary>
    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses;
        public List<RecordedRequest> Requests { get; } = [];

        public RecordingHandler(params HttpResponseMessage[] responses)
        {
            _responses = new Queue<HttpResponseMessage>(responses);
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content == null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            Requests.Add(new RecordedRequest(
                request.RequestUri!.AbsolutePath,
                request.Headers.TryGetValues("Authorization", out var values) ? string.Join(" ", values) : null,
                body));
            if (_responses.Count == 0)
                throw new InvalidOperationException("No more queued responses");
            return _responses.Dequeue();
        }
    }
}
