namespace Sellevate.Ai.Infrastructure.Configuration;

/// <summary>
/// Whisper transcription settings. The key is not here — transcription authenticates with
/// <c>OpenAI:ApiKey</c>, which is why this section carries a full <see cref="ApiUrl"/> instead of a
/// base address.
///
/// <para>
/// <see cref="MaximumFileSizeMegabytes"/> is the limit a caller is told about and is checked in the
/// controller; <see cref="RequestSizeLimitBytesDefault"/> is the larger hard ceiling Kestrel refuses
/// a body at before any of this code runs. The two are deliberately different: the outer one exists
/// so an oversized upload is rejected as a readable message rather than as a dropped connection, so
/// it must stay above the inner one.
/// </para>
/// </summary>
public sealed class WhisperConfiguration
{
    public const string SectionName = "Whisper";

    /// <summary>
    /// Kestrel's body ceiling for a transcription upload. A compile-time constant because
    /// <c>[RequestSizeLimit]</c> is an attribute argument and cannot read configuration.
    /// </summary>
    public const int RequestSizeLimitBytesDefault = 50 * 1024 * 1024;

    private const int BytesPerMegabyte = 1024 * 1024;

    public string Model { get; init; } = "whisper-1";
    public string ApiUrl { get; init; } = "https://api.openai.com/v1/audio/transcriptions";

    /// <summary>
    /// Response shape asked of the provider. The verbose form is what carries the detected
    /// <c>language</c> field this service returns to the caller; the plain form does not.
    /// </summary>
    public string ResponseFormat { get; init; } = "verbose_json";

    public string? Language { get; init; }
    public int MaximumFileSizeMegabytes { get; init; } = 25;
    public long MaximumFileSizeBytes => (long)MaximumFileSizeMegabytes * BytesPerMegabyte;
    public int RequestSizeLimitBytes { get; init; } = RequestSizeLimitBytesDefault;
}
