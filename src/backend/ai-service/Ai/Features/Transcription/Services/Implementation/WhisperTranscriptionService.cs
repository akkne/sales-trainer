using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using Sellevate.Ai.Features.Transcription.Models;
using Sellevate.Ai.Features.Quotas.Models;
using Sellevate.Ai.Features.Quotas.Services.Abstract;
using Sellevate.Ai.Features.Transcription.Services.Abstract;
using Sellevate.Ai.Infrastructure.Configuration;

namespace Sellevate.Ai.Features.Transcription.Services.Implementation;

internal sealed class WhisperTranscriptionService(
    IHttpClientFactory httpClientFactory,
    IOptions<WhisperConfiguration> whisperOptions,
    IOptions<OpenAiConfiguration> openAiOptions,
    IAiSpendMeter spendMeter,
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
            // A rejected request (bad audio, quota, auth) is an expected upstream state, not a defect here.
            if ((int)response.StatusCode >= 500)
                logger.LogError("Whisper API failed with {StatusCode}: {Body}", (int)response.StatusCode, RedactAndTruncate(errorBody));
            else
                logger.LogWarning("Whisper API rejected the request with {StatusCode}: {Body}", (int)response.StatusCode, RedactAndTruncate(errorBody));
            throw new InvalidOperationException("AI provider error");
        }

        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        JsonElement json;
        try
        {
            json = JsonDocument.Parse(responseBody).RootElement.Clone();
        }
        catch (JsonException jsonException)
        {
            logger.LogWarning(jsonException, "Whisper API returned a non-JSON body: {Body}", RedactAndTruncate(responseBody));
            throw new InvalidOperationException("AI provider returned an unreadable response");
        }

        var text = json.TryGetProperty("text", out var textElement)
            ? textElement.GetString() ?? string.Empty
            : string.Empty;

        var detectedLanguage = json.TryGetProperty("language", out var languageElement)
            ? languageElement.GetString()
            : null;

        logger.LogInformation("Whisper transcription succeeded. Language={Language}, Length={Length}",
            detectedLanguage, text.Length);

        // Phase 40.33. Recorded, deliberately not gated. Whisper bills per second of audio and this
        // endpoint never learns the duration — it forwards a file it does not decode — so the only
        // honest unit here is transcribed characters, which is a proxy good enough to see a spike in
        // the report and not good enough to refuse a call on. Recorded in docs/DONT_FORGET.md.
        await spendMeter.RecordSpeechUsageAsync(
            AiUsageKinds.Stt, configuration.Model, text.Length, cancellationToken);

        return new TranscriptionResult(text, detectedLanguage);
    }

    private static string RedactAndTruncate(string body)
    {
        const int maxLength = 500;
        var redacted = Regex.Replace(body, @"sk-[A-Za-z0-9\-_]{8,}", "[REDACTED]", RegexOptions.None, TimeSpan.FromSeconds(1));
        redacted = Regex.Replace(redacted, @"(?i)(Authorization|X-Auth-Token)\s*[:=]\s*\S+", "$1=[REDACTED]", RegexOptions.None, TimeSpan.FromSeconds(1));
        return redacted.Length > maxLength ? redacted[..maxLength] + "…" : redacted;
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
