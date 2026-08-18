using Sellevate.Ai.Features.Voice.Services.Abstract;

namespace Sellevate.Ai.Features.Voice.Services.Implementation;

/// <summary>
/// Decorates <see cref="TtsRouter"/> with an audio cache for short phrases, so repeated greetings and
/// confirmations skip the provider round-trip — and the charge — entirely.
///
/// <para>
/// Long texts pass straight through uncached: a full reply is practically never produced twice, so
/// caching one costs memory and buys nothing. The cache key pairs the voice with the text, because the
/// same words in a different persona's voice are different audio.
/// </para>
///
/// <para>
/// Callers receive a fresh non-writable <see cref="MemoryStream"/> over the cached array on every hit.
/// The array itself is shared, so it must never be mutated by a caller.
/// </para>
/// </summary>
internal sealed class CachingTtsRouter : ITtsRouter
{
    private readonly ITtsRouter _inner;
    private readonly TtsAudioCache _audioCache;
    private readonly int _maximumCacheableTextLength;

    public CachingTtsRouter(ITtsRouter inner, TtsAudioCache audioCache, int maximumCacheableTextLength)
    {
        _inner = inner;
        _audioCache = audioCache;
        _maximumCacheableTextLength = maximumCacheableTextLength;
    }

    public bool IsConfigured => _inner.IsConfigured;

    public async Task<Stream> SynthesizeSpeechAsync(string text, string? modeVoiceId, CancellationToken cancellationToken = default)
    {
        if (text.Length > _maximumCacheableTextLength)
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
