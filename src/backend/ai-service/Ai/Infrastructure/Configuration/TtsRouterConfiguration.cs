namespace Sellevate.Ai.Infrastructure.Configuration;

/// <summary>
/// Speech-synthesis routing and the audio cache in front of it, both under the <c>Voice</c> section.
///
/// <para>
/// The cache bound is expressed in bytes of audio rather than in entries because the entries differ
/// by two orders of magnitude in size: an entry count that is safe for one-word confirmations is a
/// several-hundred-megabyte process for full sentences.
/// <see cref="MaximumCacheableTextLength"/> is what keeps the population to phrases that actually
/// repeat — a long reply is never said twice, so caching it costs memory and buys nothing.
/// </para>
/// </summary>
public sealed class TtsRouterConfiguration
{
    public const string SectionName = "Voice";

    /// <summary>
    /// Provider the router selects. Note that the router currently resolves the provider from which
    /// key is configured rather than from this value — see docs/DONT_FORGET.md.
    /// </summary>
    public string TtsProvider { get; init; } = "yandex";

    /// <summary>Longest text that is worth caching, in characters.</summary>
    public int MaximumCacheableTextLength { get; init; } = 80;

    /// <summary>Total synthesized audio the process-wide cache may hold, in bytes.</summary>
    public long AudioCacheMaximumTotalBytes { get; init; } = 32L * 1024 * 1024;

    /// <summary>How long a cached phrase stays valid, in hours.</summary>
    public int AudioCacheEntryLifetimeHours { get; init; } = 24;
}
