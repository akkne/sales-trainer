using System.Text.Json;
using Microsoft.Extensions.Options;
using Sellevate.Ai.Features.Transcription.Models;
using Sellevate.Ai.Features.Transcription.Services.Abstract;
using Sellevate.Ai.Infrastructure.Configuration;

namespace Sellevate.Ai.Features.Transcription.Services.Implementation;

internal sealed class WhisperTranscriptionService(
    IHttpClientFactory httpClientFactory,
    IOptions<WhisperConfiguration> whisperOptions,
    IOptions<OpenAiConfiguration> openAiOptions,
    ILogger<WhisperTranscriptionService> logger) : ITranscriptionService
{
    public async Task<TranscriptionResult> TranscribeAsync(
        Stream audioStream,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        var apiKey = openAiOptions.Value.ApiKey;
        if (string.IsNullOrEmpty(apiKey) || apiKey.StartsWith("REPLACE_"))
        {
            logger.LogWarning("OpenAI API key is not configured. Returning stub transcription.");
            return new TranscriptionResult("Транскрипция недоступна — ключ OpenAI не настроен.", null);
        }

        var configuration = whisperOptions.Value;
        var httpClient = httpClientFactory.CreateClient("OpenAI");
        httpClient.DefaultRequestHeaders.Remove("Authorization");
        httpClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);

        using var form = new MultipartFormDataContent();

        var fileContent = new StreamContent(audioStream);
        fileContent.Headers.ContentType =
            new System.Net.Http.Headers.MediaTypeHeaderValue(GetMimeType(fileName));
        form.Add(fileContent, "file", fileName);
        form.Add(new StringContent(configuration.Model), "model");
        form.Add(new StringContent("verbose_json"), "response_format");

        if (!string.IsNullOrEmpty(configuration.Language))
            form.Add(new StringContent(configuration.Language), "language");

        logger.LogInformation("Sending audio file {FileName} to Whisper API (model={Model})", fileName, configuration.Model);

        using var response = await httpClient.PostAsync(configuration.ApiUrl, form, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            logger.LogError("Whisper API returned {StatusCode}: {Body}", (int)response.StatusCode, errorBody);
            throw new InvalidOperationException(
                $"Whisper API error {(int)response.StatusCode}: {errorBody}");
        }

        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        var json = JsonDocument.Parse(responseBody).RootElement;

        var text = json.TryGetProperty("text", out var textElement)
            ? textElement.GetString() ?? string.Empty
            : string.Empty;

        var detectedLanguage = json.TryGetProperty("language", out var languageElement)
            ? languageElement.GetString()
            : null;

        logger.LogInformation("Whisper transcription succeeded. Language={Language}, Length={Length}",
            detectedLanguage, text.Length);

        return new TranscriptionResult(text, detectedLanguage);
    }

    private static string GetMimeType(string fileName)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        return extension switch
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
