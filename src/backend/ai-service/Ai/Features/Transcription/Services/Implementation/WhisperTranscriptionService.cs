using Microsoft.Extensions.Options;
using Sellevate.Ai.Common.Constants;
using Sellevate.Ai.Common.Implementation;
using Sellevate.Ai.Features.Quotas.Models;
using Sellevate.Ai.Features.Quotas.Services.Abstract;
using Sellevate.Ai.Features.Transcription.Constants;
using Sellevate.Ai.Features.Transcription.Models;
using Sellevate.Ai.Features.Transcription.Services.Abstract;
using Sellevate.Ai.Infrastructure.Configuration;
using System.Net.Http.Headers;
using System.Text.Json;

namespace Sellevate.Ai.Features.Transcription.Services.Implementation;

/// <summary>
/// Transcription against Whisper. Authenticates with <c>OpenAI:ApiKey</c> — there is no separate
/// Whisper credential, which is why the section carries a full URL rather than a base address.
///
/// <para>
/// <b>An unconfigured key returns a stub transcript, not an error.</b> Transcription is an input
/// method rather than a feature of its own, so a deployment without a provider key should let the
/// learner type instead of failing the page.
/// </para>
///
/// <para>
/// <b>Usage is recorded and deliberately not gated</b> (Phase 40.33). Whisper bills per second of
/// audio and this service never learns the duration — it forwards a file it does not decode — so the
/// only honest unit here is transcribed characters. That is a good enough proxy to see a spike on the
/// spend report and not good enough to refuse a call on. Recorded in <c>docs/DONT_FORGET.md</c>.
/// </para>
///
/// <para>
/// A provider rejection is logged as a warning and only a 5xx as an error, so a bad upload does not
/// read as a defect here. Either way the body is redacted before it reaches the log — a provider
/// echoing our request back can otherwise put the key in the log.
/// </para>
/// </summary>
internal sealed class WhisperTranscriptionService(
    IHttpClientFactory httpClientFactory,
    IOptions<WhisperConfiguration> whisperOptions,
    IOptions<OpenAiConfiguration> openAiOptions,
    IAiSpendMeter spendMeter,
    ILogger<WhisperTranscriptionService> logger) : ITranscriptionService
{
    private const string FileFormFieldName = "file";
    private const string ModelFormFieldName = "model";
    private const string ResponseFormatFormFieldName = "response_format";
    private const string LanguageFormFieldName = "language";
    private const string TextPropertyName = "text";
    private const string LanguagePropertyName = "language";
    private const int LowestServerErrorStatusCode = 500;

    public async Task<TranscriptionResult> TranscribeAsync(
        Stream audioStream,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        var apiKey = openAiOptions.Value.ApiKey;
        if (!AiSecretPlaceholders.IsRealSecret(apiKey))
        {
            logger.LogWarning("OpenAI API key is not configured. Returning stub transcription.");
            return new TranscriptionResult(TranscriptionMessages.NotConfiguredTranscript, null);
        }

        var configuration = whisperOptions.Value;
        var httpClient = httpClientFactory.CreateClient(AiProviderHttpConstants.OpenAiClientName);
        httpClient.DefaultRequestHeaders.Remove(AiProviderHttpConstants.AuthorizationHeaderName);
        httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(AiProviderHttpConstants.BearerScheme, apiKey);

        using var form = new MultipartFormDataContent();

        var fileContent = new StreamContent(audioStream);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(ResolveMimeType(fileName));
        form.Add(fileContent, FileFormFieldName, fileName);
        form.Add(new StringContent(configuration.Model), ModelFormFieldName);
        form.Add(new StringContent(configuration.ResponseFormat), ResponseFormatFormFieldName);

        if (!string.IsNullOrEmpty(configuration.Language))
            form.Add(new StringContent(configuration.Language), LanguageFormFieldName);

        logger.LogInformation("Sending audio file {FileName} to Whisper API (model={Model})", fileName, configuration.Model);

        using var response = await httpClient.PostAsync(configuration.ApiUrl, form, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            if ((int)response.StatusCode >= LowestServerErrorStatusCode)
                logger.LogError("Whisper API failed with {StatusCode}: {Body}", (int)response.StatusCode, ProviderResponseRedactor.RedactAndTruncate(errorBody));
            else
                logger.LogWarning("Whisper API rejected the request with {StatusCode}: {Body}", (int)response.StatusCode, ProviderResponseRedactor.RedactAndTruncate(errorBody));
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
            logger.LogWarning(jsonException, "Whisper API returned a non-JSON body: {Body}", ProviderResponseRedactor.RedactAndTruncate(responseBody));
            throw new InvalidOperationException("AI provider returned an unreadable response");
        }

        var text = json.TryGetProperty(TextPropertyName, out var textElement)
            ? textElement.GetString() ?? string.Empty
            : string.Empty;

        var detectedLanguage = json.TryGetProperty(LanguagePropertyName, out var languageElement)
            ? languageElement.GetString()
            : null;

        logger.LogInformation("Whisper transcription succeeded. Language={Language}, Length={Length}",
            detectedLanguage, text.Length);

        await spendMeter.RecordSpeechUsageAsync(
            AiUsageKinds.Stt, configuration.Model, text.Length, cancellationToken);

        return new TranscriptionResult(text, detectedLanguage);
    }

    private static string ResolveMimeType(string fileName)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        return TranscriptionAudioFormats.MimeTypesByExtension.TryGetValue(extension, out var mimeType)
            ? mimeType
            : TranscriptionAudioFormats.FallbackMimeType;
    }
}
