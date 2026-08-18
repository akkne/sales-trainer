using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Sellevate.Ai.Infrastructure.Configuration;

namespace Sellevate.Ai.Features.Voice.Services.Implementation;

/// <summary>
/// Process-wide cache for synthesized audio of short, frequently repeated phrases (greetings,
/// confirmations, refusals).
///
/// <para>
/// Bounded by total audio bytes rather than by entry count: entries differ in size by two orders of
/// magnitude, so an entry limit that is safe for one-word confirmations would let full sentences grow
/// the process by hundreds of megabytes. Singleton by design — the saving comes from phrases repeating
/// across sessions and services, which a per-request cache cannot see.
/// </para>
///
/// <para>
/// Keys are opaque to this class. The caller is responsible for including the voice in the key;
/// caching by text alone would hand one persona another's voice.
/// </para>
/// </summary>
internal sealed class TtsAudioCache : IDisposable
{
    private readonly MemoryCache _cache;
    private readonly TimeSpan _entryLifetime;

    public TtsAudioCache(IOptions<TtsRouterConfiguration> ttsRouterOptions)
    {
        var configuration = ttsRouterOptions.Value;
        _cache = new MemoryCache(new MemoryCacheOptions { SizeLimit = configuration.AudioCacheMaximumTotalBytes });
        _entryLifetime = TimeSpan.FromHours(configuration.AudioCacheEntryLifetimeHours);
    }

    public bool TryGet(string key, out byte[] audio)
    {
        if (_cache.TryGetValue(key, out byte[]? cached) && cached is not null)
        {
            audio = cached;
            return true;
        }

        audio = [];
        return false;
    }

    public void Set(string key, byte[] audio)
    {
        _cache.Set(key, audio, new MemoryCacheEntryOptions
        {
            Size = audio.Length,
            AbsoluteExpirationRelativeToNow = _entryLifetime,
        });
    }

    public void Dispose() => _cache.Dispose();
}
