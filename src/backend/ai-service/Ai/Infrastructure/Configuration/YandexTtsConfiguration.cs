namespace Sellevate.Ai.Infrastructure.Configuration;

/// <summary>
/// Yandex SpeechKit synthesis settings. <c>ApiKey</c> is injected from the root <c>.env</c> and is
/// never present in <c>appsettings.json</c> as anything but the <c>INJECTED_FROM_ENV</c> marker.
///
/// <para>
/// Everything else here is operational tuning a person may want to change without a rebuild: the
/// voice, the language, the request path and the audio parameters. <see cref="SampleRateHertz"/> and
/// <see cref="AudioFormat"/> are paired — the response is raw LPCM that this service wraps in a WAV
/// header, and a header that disagrees with the rate the provider actually sampled at plays back at
/// the wrong pitch rather than failing.
/// </para>
/// </summary>
public sealed class YandexTtsConfiguration
{
    public const string SectionName = "YandexTts";
    public required string ApiKey { get; init; }
    public string BaseUrl { get; init; } = "https://tts.api.cloud.yandex.net";

    /// <summary>Path appended to <see cref="BaseUrl"/> for a synthesis request.</summary>
    public string SynthesizePath { get; init; } = "/speech/v1/tts:synthesize";

    public string Voice { get; init; } = "marina";
    public string Lang { get; init; } = "ru-RU";
    public string? Role { get; init; }
    public string? Speed { get; init; }
    public string? FolderId { get; init; }

    /// <summary>
    /// Container the provider is asked for. Raw LPCM, because this service wraps the bytes in a WAV
    /// header itself — asking for a container would mean parsing one back out to do the same.
    /// </summary>
    public string AudioFormat { get; init; } = "lpcm";

    public int SampleRateHertz { get; init; } = 48000;

    /// <summary>
    /// Name this provider is billed under on the spend report, and the key its price is looked up by
    /// in <c>AiQuotas:PricePerMillionTokens</c>. Renaming it without renaming the price-table key
    /// silently prices synthesis at the fallback price.
    /// </summary>
    public string MeteredModelName { get; init; } = "yandex-tts";
}
