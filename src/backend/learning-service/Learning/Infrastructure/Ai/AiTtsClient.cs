using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Sellevate.BuildingBlocks.Tenancy;
using Sellevate.Learning.Infrastructure.Configuration;

namespace Sellevate.Learning.Infrastructure.Ai;

/// <summary>
/// Phase 40.33. Speech synthesis over the wire, replacing learning-service's own copies of
/// <c>TtsRouter</c> and <c>YandexTtsService</c>.
///
/// <para>
/// The LLM copy is the one <c>docs/DONT_FORGET.md</c> named, but the speech copy was the sharper
/// hole: voice minutes were already metered per organization in ai-service since 40.11, and every
/// sentence the interactive <c>ai_dialogue</c> exercise spoke was synthesized here, outside that
/// meter, against a Yandex key this service held its own copy of. A customer practising exercises all
/// day spent speech budget that no counter in the product could see.
/// </para>
///
/// <para>
/// Going through ai-service also buys the cache: <c>CachingTtsRouter</c> keeps short phrases in
/// memory, so a greeting an exercise and a roleplay both produce is now synthesized once for the
/// installation rather than once per service.
/// </para>
///
/// <para>
/// The failure contract is unchanged and is the reason this returns a stream rather than throwing on
/// a bad status: <c>ExerciseDialogService.TrySynthesizeAsync</c> swallows a synthesis failure and
/// delivers the reply as text, so what matters is that a failure *is* an exception and not a stream
/// of zero bytes that reaches the client as silence.
/// </para>
/// </summary>
internal sealed class AiTtsClient(
    HttpClient httpClient,
    IOptions<AiServiceConfiguration> configurationOptions,
    ITenantContext tenantContext) : ITtsRouter
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly AiServiceConfiguration _configuration = configurationOptions.Value;

    /// <summary>
    /// Always true, for the reason <see cref="AiChatClient.IsConfigured"/> gives: whether a speech key
    /// exists is ai-service's fact, it answers 503 when it does not, and this path already degrades
    /// a synthesis failure into a text-only reply.
    /// </summary>
    public bool IsConfigured => true;

    public async Task<Stream> SynthesizeSpeechAsync(
        string text,
        string? modeVoiceId,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            _configuration.BaseUrl.TrimEnd('/') + _configuration.TextToSpeechPath)
        {
            Content = JsonContent.Create(new AiSynthesisRequest(text, modeVoiceId), options: SerializerOptions),
        };

        AiCallHeaders.Apply(request, tenantContext, AiCallHeaders.InteractiveWorkload);

        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var audio = new MemoryStream();
        await response.Content.CopyToAsync(audio, cancellationToken);
        audio.Position = 0;

        return audio;
    }

    private sealed record AiSynthesisRequest(string Text, string? VoiceId);
}
