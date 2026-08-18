namespace Sellevate.Ai.Common.Constants;

/// <summary>
/// The user-facing text for each way a paid provider can refuse us, stated once.
///
/// <para>
/// These strings were repeated verbatim in every action that touches the model — three ladders in
/// <c>DialogController</c> alone — which is how a service ends up telling a learner "rate limit"
/// on one screen and "temporarily unavailable" on another for the identical upstream failure. The
/// wording deliberately never echoes the provider's own body: that body is redacted and dropped at
/// the client, and it must not start travelling again through an error message.
/// </para>
/// </summary>
public static class AiProviderFailureMessages
{
    /// <summary>Provider answered 402. Nothing the caller can retry into.</summary>
    public const string PaymentRequired = "AI service requires payment. Please check your API balance.";

    /// <summary>Provider answered 429. Retrying later is the correct response.</summary>
    public const string RateLimited = "AI service rate limit exceeded. Please try again later.";

    /// <summary>Provider answered 401/403 — our configuration, never the caller's request.</summary>
    public const string AuthenticationFailed = "AI service authentication failed.";

    /// <summary>Provider rejected the request or answered with something unusable.</summary>
    public const string TemporarilyUnavailable = "AI service is temporarily unavailable. Please try again.";

    /// <summary>No provider key is configured at all, so the feature is off rather than broken.</summary>
    public const string NotConfigured = "AI service is not configured";
}
