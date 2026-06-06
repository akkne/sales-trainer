using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;
using NUnit.Framework;
using SalesTrainer.Api.Features.Voice;
using SalesTrainer.Api.Features.Voice.Services.Abstract;
using SalesTrainer.Api.Features.Voice.Services.Implementation;
using SalesTrainer.Api.Infrastructure.Configuration;

namespace SalesTrainer.Tests.Unit;

[TestFixture]
public class TtsRouterTests
{
    private IYandexTtsService _yandex = null!;
    private IVoicerTtsService _voicer = null!;
    private IGoogleTtsService _google = null!;

    [SetUp]
    public void SetUp()
    {
        _yandex = Substitute.For<IYandexTtsService>();
        _voicer = Substitute.For<IVoicerTtsService>();
        _google = Substitute.For<IGoogleTtsService>();
    }

    private TtsRouter CreateRouter(string? preferredProvider)
    {
        var options = Options.Create(preferredProvider == null
            ? new TtsRouterConfiguration()
            : new TtsRouterConfiguration { TtsProvider = preferredProvider });
        return new TtsRouter(_yandex, _voicer, _google, options);
    }

    [Test]
    public void IsConfigured_FalseWhenNoProviderHasCredentials()
    {
        CreateRouter("yandex").IsConfigured.Should().BeFalse();
        CreateRouter(null).IsConfigured.Should().BeFalse();
    }

    [Test]
    public void PreferredProvider_UsedWhenConfigured()
    {
        _voicer.IsConfigured.Returns(true);
        _yandex.IsConfigured.Returns(true);

        var router = CreateRouter("voicer");

        router.IsConfigured.Should().BeTrue();
        router.IsRealtime.Should().BeFalse();
    }

    [Test]
    public void FallsBackToYandex_WhenPreferredProviderNotConfigured()
    {
        _yandex.IsConfigured.Returns(true);

        var router = CreateRouter("voicer");

        router.IsConfigured.Should().BeTrue();
        router.IsRealtime.Should().BeTrue();
    }

    [Test]
    public void FallsBackToVoicer_WhenOnlyVoicerConfigured()
    {
        _voicer.IsConfigured.Returns(true);

        var router = CreateRouter("yandex");

        router.IsConfigured.Should().BeTrue();
        router.IsRealtime.Should().BeFalse();
    }

    [Test]
    public void DefaultsToYandex_WhenNoProviderSpecified()
    {
        _yandex.IsConfigured.Returns(true);
        _voicer.IsConfigured.Returns(true);

        var router = CreateRouter(null);

        router.IsRealtime.Should().BeTrue();
    }

    [Test]
    public async Task Synthesize_RoutesToYandex_WithoutModeVoiceId()
    {
        _yandex.IsConfigured.Returns(true);
        _yandex.SynthesizeSpeechAsync("Привет", null, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Stream>(new MemoryStream([1, 2, 3])));

        var router = CreateRouter("yandex");
        await using var stream = await router.SynthesizeSpeechAsync("Привет", "elevenlabs-voice-id");

        // ElevenLabs mode voice ids must not leak into Yandex requests.
        await _yandex.Received(1).SynthesizeSpeechAsync("Привет", null, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Synthesize_RoutesToVoicer_WithModeVoiceId()
    {
        _voicer.IsConfigured.Returns(true);
        _voicer.SynthesizeSpeechAsync("Привет", "voice-1", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Stream>(new MemoryStream([1, 2, 3])));

        var router = CreateRouter("voicer");
        await using var stream = await router.SynthesizeSpeechAsync("Привет", "voice-1");

        await _voicer.Received(1).SynthesizeSpeechAsync("Привет", "voice-1", Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Synthesize_NoProviderConfigured_Throws()
    {
        var router = CreateRouter(null);

        var act = () => router.SynthesizeSpeechAsync("Привет", null);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}
