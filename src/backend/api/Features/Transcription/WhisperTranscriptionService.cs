using System.Text.Json;

namespace SalesTrainer.Api.Features.Transcription;

public class WhisperTranscriptionService(
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    ILogger<WhisperTranscriptionService> logger) : ITranscriptionService
{
    private const string DefaultWhisperApiUrl = "https://api.openai.com/v1/audio/transcriptions";

    public async Task<TranscriptionResult> TranscribeAsync(
        Stream audioStream,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        var apiKey = configuration["OpenAI:ApiKey"];
        if (string.IsNullOrEmpty(apiKey) || apiKey.StartsWith("REPLACE_"))
        {
            logger.LogWarning("OpenAI API key is not configured. Returning stub transcription.");
            return new TranscriptionResult("Транскрипция недоступна — ключ OpenAI не настроен.", null);
        }

        var model = configuration["Whisper:Model"] ?? "whisper-1";
        var language = configuration["Whisper:Language"];
        var whisperApiUrl = configuration["Whisper:ApiUrl"] ?? DefaultWhisperApiUrl;

        var httpClient = httpClientFactory.CreateClient("OpenAI");
        httpClient.DefaultRequestHeaders.Remove("Authorization");
        httpClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);

        using var form = new MultipartFormDataContent();

        var fileContent = new StreamContent(audioStream);
        fileContent.Headers.ContentType =
            new System.Net.Http.Headers.MediaTypeHeaderValue(GetMimeType(fileName));
        form.Add(fileContent, "file", fileName);
        form.Add(new StringContent(model), "model");
        form.Add(new StringContent("verbose_json"), "response_format");

        if (!string.IsNullOrEmpty(language))
            form.Add(new StringContent(language), "language");

        logger.LogInformation("Sending audio file {FileName} to Whisper API (model={Model})", fileName, model);

        using var response = await httpClient.PostAsync(whisperApiUrl, form, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            logger.LogError("Whisper API returned {StatusCode}: {Body}", (int)response.StatusCode, errorBody);
            throw new InvalidOperationException(
                $"Whisper API error {(int)response.StatusCode}: {errorBody}");
        }

        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        var json = JsonDocument.Parse(responseBody).RootElement;

        var text = json.TryGetProperty("text", out var textEl)
            ? textEl.GetString() ?? string.Empty
            : string.Empty;

        var detectedLanguage = json.TryGetProperty("language", out var langEl)
            ? langEl.GetString()
            : null;

        logger.LogInformation("Whisper transcription succeeded. Language={Language}, Length={Len}",
            detectedLanguage, text.Length);

        return new TranscriptionResult(text, detectedLanguage);
    }

    private static string GetMimeType(string fileName)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ext switch
        {
            ".mp3"  => "audio/mpeg",
            ".mp4"  => "audio/mp4",
            ".m4a"  => "audio/mp4",
            ".mpeg" => "audio/mpeg",
            ".mpga" => "audio/mpeg",
            ".wav"  => "audio/wav",
            ".webm" => "audio/webm",
            ".ogg"  => "audio/ogg",
            _       => "application/octet-stream"
        };
    }
}
