using Sellevate.Ai.Features.Voice.Services.Abstract;

namespace Sellevate.Ai.Features.Voice.Services.Implementation;

/// <summary>
/// Decorates <see cref="TtsRouter"/> with an audio cache for short phrases, so
/// repeated greetings/confirmations skip the provider round-trip entirely.
/// Long texts are passed through uncached — they are practically never repeated.
/// </summary>
internal sealed class CachingTtsRouter : ITtsRouter
{
    private const int MaximumCacheableTextLength = 80;

    private readonly ITtsRouter _inner;
    private readonly TtsAudioCache _audioCache;

    public CachingTtsRouter(ITtsRouter inner, TtsAudioCache audioCache)
    {
        _inner = inner;
        _audioCache = audioCache;
    }

    public bool IsConfigured => _inner.IsConfigured;

    public async Task<Stream> SynthesizeSpeechAsync(string text, string? modeVoiceId, CancellationToken cancellationToken = default)
    {
        if (text.Length > MaximumCacheableTextLength)
            return await _inner.SynthesizeSpeechAsync(text, modeVoiceId, cancellationToken);

        var cacheKey = $"{modeVoiceId}\n{text}";
        if (_audioCache.TryGet(cacheKey, out var cachedAudio))
            return new MemoryStream(cachedAudio, writable: false);

        var synthesized = await _inner.SynthesizeSpeechAsync(text, modeVoiceId, cancellationToken);
        await using (synthesized)
        {
            using var buffer = new MemoryStream();
            await synthesized.CopyToAsync(buffer, cancellationToken);
            var audio = buffer.ToArray();
            _audioCache.Set(cacheKey, audio);
            return new MemoryStream(audio, writable: false);
        }
    }
}
