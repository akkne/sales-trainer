using FluentAssertions;
using NSubstitute;
using NUnit.Framework;
using SalesTrainer.Api.Features.Voice.Services.Abstract;
using SalesTrainer.Api.Features.Voice.Services.Implementation;

namespace SalesTrainer.Tests.Unit;

[TestFixture]
public class CachingTtsRouterTests
{
    private ITtsRouter _inner = null!;
    private TtsAudioCache _audioCache = null!;
    private CachingTtsRouter _router = null!;

    [SetUp]
    public void SetUp()
    {
        _inner = Substitute.For<ITtsRouter>();
        _inner.SynthesizeSpeechAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult<Stream>(new MemoryStream([1, 2, 3])));
        _audioCache = new TtsAudioCache();
        _router = new CachingTtsRouter(_inner, _audioCache);
    }

    [TearDown]
    public void TearDown()
    {
        _audioCache.Dispose();
    }

    private static async Task<byte[]> ReadAllAsync(Stream stream)
    {
        await using (stream)
        {
            using var buffer = new MemoryStream();
            await stream.CopyToAsync(buffer);
            return buffer.ToArray();
        }
    }

    [Test]
    public async Task ShortPhrase_SecondCallServedFromCache()
    {
        var first = await ReadAllAsync(await _router.SynthesizeSpeechAsync("Добрый день!", "voice-1"));
        var second = await ReadAllAsync(await _router.SynthesizeSpeechAsync("Добрый день!", "voice-1"));

        first.Should().Equal(1, 2, 3);
        second.Should().Equal(first);
        await _inner.Received(1).SynthesizeSpeechAsync("Добрый день!", "voice-1", Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task LongText_BypassesCache()
    {
        var longText = new string('а', 81);

        await ReadAllAsync(await _router.SynthesizeSpeechAsync(longText, "voice-1"));
        await ReadAllAsync(await _router.SynthesizeSpeechAsync(longText, "voice-1"));

        await _inner.Received(2).SynthesizeSpeechAsync(longText, "voice-1", Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task DifferentVoices_GetSeparateCacheEntries()
    {
        await ReadAllAsync(await _router.SynthesizeSpeechAsync("Добрый день!", "voice-1"));
        await ReadAllAsync(await _router.SynthesizeSpeechAsync("Добрый день!", "voice-2"));

        await _inner.Received(1).SynthesizeSpeechAsync("Добрый день!", "voice-1", Arg.Any<CancellationToken>());
        await _inner.Received(1).SynthesizeSpeechAsync("Добрый день!", "voice-2", Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ProviderFailure_IsNotCached()
    {
        _inner.SynthesizeSpeechAsync("Алло?", null, Arg.Any<CancellationToken>())
            .Returns(
                _ => Task.FromException<Stream>(new InvalidOperationException("provider down")),
                _ => Task.FromResult<Stream>(new MemoryStream([7])));

        var firstAttempt = () => _router.SynthesizeSpeechAsync("Алло?", null);
        await firstAttempt.Should().ThrowAsync<InvalidOperationException>();

        var recovered = await ReadAllAsync(await _router.SynthesizeSpeechAsync("Алло?", null));
        recovered.Should().Equal(7);
    }

    [Test]
    public void IsConfigured_DelegatesToInnerRouter()
    {
        _inner.IsConfigured.Returns(true);
        _router.IsConfigured.Should().BeTrue();

        _inner.IsConfigured.Returns(false);
        _router.IsConfigured.Should().BeFalse();
    }
}
