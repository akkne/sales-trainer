namespace Sellevate.Ai.Common.Constants;

/// <summary>
/// The provider-facing HTTP plumbing that has to agree between the typed-client registrations in
/// <c>Program.cs</c> and every service that resolves one of those clients.
///
/// <para>
/// A client name that drifts does not fail loudly: <see cref="IHttpClientFactory"/> hands back a
/// default <see cref="HttpClient"/> with none of the configured timeout, retry policy or base
/// address, so the mistake surfaces as an unbounded hang against a paid provider rather than as an
/// error. The same holds for the authentication header names — the two providers disagree about
/// which header carries the key, and getting it wrong reads as a 401 from the provider rather than
/// as a bug here.
/// </para>
/// </summary>
public static class AiProviderHttpConstants
{
    /// <summary>Named client for every OpenAI-compatible call: chat completions and Whisper.</summary>
    public const string OpenAiClientName = "OpenAI";

    /// <summary>Named client for Yandex SpeechKit synthesis.</summary>
    public const string YandexTtsClientName = "YandexTts";

    public const string AuthorizationHeaderName = "Authorization";

    /// <summary>Scheme standard OpenAI expects in <c>Authorization</c>.</summary>
    public const string BearerScheme = "Bearer";

    /// <summary>Scheme Yandex SpeechKit expects in <c>Authorization</c>.</summary>
    public const string YandexApiKeyScheme = "Api-Key";

    /// <summary>
    /// Header the F5 AI gateway carries the key in instead of <c>Authorization</c>. Both are removed
    /// before either is set, so switching provider never leaves the other one attached.
    /// </summary>
    public const string F5AuthenticationTokenHeaderName = "X-Auth-Token";

    /// <summary>
    /// Tells an nginx-class reverse proxy not to buffer the response. Without it a proxy holds a
    /// streamed dialog turn until it completes, which turns incremental audio into one late blob.
    /// </summary>
    public const string AccelBufferingHeaderName = "X-Accel-Buffering";

    /// <summary>Value of <see cref="AccelBufferingHeaderName"/> that disables buffering.</summary>
    public const string AccelBufferingDisabled = "no";

    /// <summary><c>Cache-Control</c> value on every streamed response.</summary>
    public const string NoCacheDirective = "no-cache";
}
