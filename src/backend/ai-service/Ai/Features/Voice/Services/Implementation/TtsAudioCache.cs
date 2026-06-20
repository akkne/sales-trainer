using Microsoft.Extensions.Caching.Memory;

namespace Sellevate.Ai.Features.Voice.Services.Implementation;

/// <summary>
/// Process-wide cache for synthesized audio of short, frequently repeated phrases
/// (greetings, confirmations, refusals). Bounded by total audio size so it can
/// never grow past a few dozen megabytes.
/// </summary>
internal sealed class TtsAudioCache : IDisposable
{
    private const long MaximumTotalBytes = 32 * 1024 * 1024;
    private static readonly TimeSpan EntryLifetime = TimeSpan.FromHours(24);

    private readonly MemoryCache _cache = new(new MemoryCacheOptions { SizeLimit = MaximumTotalBytes });

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
            AbsoluteExpirationRelativeToNow = EntryLifetime,
        });
    }

    public void Dispose() => _cache.Dispose();
}
