using System.Net;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using NUnit.Framework;
using SalesTrainer.Api.Infrastructure.Configuration;
using SalesTrainer.Api.Infrastructure.Http;

namespace SalesTrainer.Tests.Unit;

[TestFixture]
public class UpstreamConnectionWarmupTests
{
    private sealed class RecordingHandler : HttpMessageHandler
    {
        public List<Uri> Requests { get; } = [];
        public bool ThrowOnSend { get; set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request.RequestUri!);
            if (ThrowOnSend)
                throw new HttpRequestException("connection refused");
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }

    private RecordingHandler _handler = null!;
    private IHttpClientFactory _httpClientFactory = null!;
    private readonly List<HttpClient> _clients = [];

    [SetUp]
    public void SetUp()
    {
        _handler = new RecordingHandler();
        _httpClientFactory = Substitute.For<IHttpClientFactory>();
        _httpClientFactory.CreateClient(Arg.Any<string>()).Returns(_ =>
        {
            var client = new HttpClient(_handler, disposeHandler: false);
            _clients.Add(client);
            return client;
        });
    }

    [TearDown]
    public void TearDown()
    {
        _clients.ForEach(c => c.Dispose());
        _clients.Clear();
        _handler.Dispose();
    }

    private UpstreamConnectionWarmup CreateWarmup(string openAiKey, string yandexKey, string googleKey)
    {
        return new UpstreamConnectionWarmup(
            _httpClientFactory,
            Options.Create(new OpenAiConfiguration { ApiKey = openAiKey, BaseUrl = "https://api.openai.example" }),
            Options.Create(new YandexTtsConfiguration { ApiKey = yandexKey, BaseUrl = "https://tts.yandex.example" }),
            Options.Create(new GoogleTtsConfiguration { ApiKey = googleKey }),
            NullLogger<UpstreamConnectionWarmup>.Instance);
    }

    [Test]
    public async Task WarmsEveryConfiguredUpstream()
    {
        var warmup = CreateWarmup("sk-test", "yc-test", "g-test");

        var warmedCount = await warmup.WarmupOnceAsync(CancellationToken.None);

        warmedCount.Should().Be(3);
        _handler.Requests.Select(u => u.Host).Should().BeEquivalentTo(
            "api.openai.example", "tts.yandex.example", "texttospeech.googleapis.com");
    }

    [Test]
    public async Task SkipsUpstreamsWithPlaceholderOrEmptyKeys()
    {
        var warmup = CreateWarmup("sk-test", "REPLACE_WITH_YANDEX_API_KEY", "");

        var warmedCount = await warmup.WarmupOnceAsync(CancellationToken.None);

        warmedCount.Should().Be(1);
        _handler.Requests.Should().ContainSingle().Which.Host.Should().Be("api.openai.example");
    }

    [Test]
    public async Task RequestFailures_AreSwallowed()
    {
        _handler.ThrowOnSend = true;
        var warmup = CreateWarmup("sk-test", "yc-test", "g-test");

        var act = () => warmup.WarmupOnceAsync(CancellationToken.None);

        await act.Should().NotThrowAsync();
        _handler.Requests.Should().HaveCount(3);
    }

    [Test]
    public async Task CallerCancellation_IsPropagated()
    {
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();
        var warmup = CreateWarmup("sk-test", "yc-test", "g-test");

        var act = () => warmup.WarmupOnceAsync(cancelled.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
