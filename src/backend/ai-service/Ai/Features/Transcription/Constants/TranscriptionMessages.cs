namespace Sellevate.Ai.Features.Transcription.Constants;

/// <summary>
/// The Russian text the transcription endpoint answers a rejected or failed upload with. User-facing
/// copy, so it belongs beside the endpoint rather than inside it, and it is deliberately vague about
/// the provider — the caller can act on "try a smaller file", never on a provider status code.
/// </summary>
public static class TranscriptionMessages
{
    public const string FileMissingOrEmpty = "Аудиофайл не передан или пустой.";
    public const string CancelledByClient = "Запрос отменён клиентом.";
    public const string ProviderFailed = "Ошибка при обращении к Whisper API.";

    /// <summary>Returned as the transcript when no provider key is configured, in place of an error.</summary>
    public const string NotConfiguredTranscript = "Транскрипция недоступна — ключ OpenAI не настроен.";

    /// <summary>Formats the size rejection. Takes the configured limit in megabytes.</summary>
    public static string FileTooLarge(int maximumMegabytes)
        => $"Размер файла превышает {maximumMegabytes} МБ.";

    /// <summary>Formats the format rejection, naming what was sent and what is accepted.</summary>
    public static string UnsupportedFormat(string extension, IEnumerable<string> acceptedExtensions)
        => $"Формат {extension} не поддерживается. Допустимые: {string.Join(", ", acceptedExtensions)}.";
}
