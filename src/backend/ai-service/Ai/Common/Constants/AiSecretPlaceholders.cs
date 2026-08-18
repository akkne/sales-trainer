namespace Sellevate.Ai.Common.Constants;

/// <summary>
/// Recognises a provider key that is a placeholder rather than a secret, so a deployment that never
/// had one injected degrades to "the feature is off" instead of authenticating with a stand-in.
///
/// <para>
/// Two placeholder families exist and both must be recognised. <c>.env.example</c> ships keys as
/// <c>REPLACE_WITH_…</c>, and every <c>appsettings.json</c> marks a secret that arrives from the
/// environment as <c>INJECTED_FROM_ENV</c>. Recognising only the first family made a deployment with
/// a missing environment variable read as <b>configured</b> and send the marker string to the
/// provider as the credential — every dialog turn, voice turn and transcription answering 401 from a
/// paid endpoint instead of degrading to an empty bundle list, text-only voice and a stub transcript.
/// The marker is a committed constant, so it is never a usable credential by construction.
/// </para>
/// </summary>
public static class AiSecretPlaceholders
{
    /// <summary>
    /// Prefix every placeholder key in <c>.env.example</c> carries. Matched case-insensitively, so
    /// neither <c>REPLACE_WITH_OPENAI_API_KEY</c> nor a hand-typed lowercase variant is mistaken for
    /// a real key.
    /// </summary>
    public const string ReplacePrefix = "REPLACE";

    /// <summary>
    /// The marker every <c>appsettings.json</c> uses for a value the environment is expected to
    /// supply. See <c>docs/CONFIGURATION.md</c>.
    /// </summary>
    public const string InjectedFromEnvironmentMarker = "INJECTED_FROM_ENV";

    /// <summary>
    /// <see langword="true"/> when <paramref name="secret"/> looks like a usable credential —
    /// non-blank and neither placeholder family.
    /// </summary>
    public static bool IsRealSecret(string? secret)
        => !string.IsNullOrWhiteSpace(secret)
           && !secret.StartsWith(ReplacePrefix, StringComparison.OrdinalIgnoreCase)
           && !secret.Equals(InjectedFromEnvironmentMarker, StringComparison.OrdinalIgnoreCase);
}
