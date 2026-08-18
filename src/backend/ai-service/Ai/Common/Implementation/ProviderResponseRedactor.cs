using System.Text.RegularExpressions;

namespace Sellevate.Ai.Common.Implementation;

/// <summary>
/// Makes a paid provider's response body safe to write to a log line, then cuts it to a length a log
/// aggregator will accept.
///
/// <para>
/// It is one routine rather than one per call path on purpose. This was copied verbatim into the three
/// services that talk to a provider — chat completions, Whisper transcription and the evaluation
/// graders — and a redaction pattern added to one copy silently left the other two leaking. A provider
/// echoes the request back inside its own error bodies often enough that these lines are the most
/// likely place a key reaches disk, so the set of patterns has to be single-sourced.
/// </para>
///
/// <para>
/// Both patterns run with a one-second match timeout: the input is attacker-influenceable text of
/// unbounded length, and a redactor that hangs is worse than a log line nobody reads.
/// </para>
/// </summary>
public static class ProviderResponseRedactor
{
    /// <summary>
    /// Longest body kept in a log line. Beyond this the tail is dropped and an ellipsis appended —
    /// provider error bodies are occasionally megabytes of HTML from a proxy in front of them.
    /// </summary>
    public const int MaximumLoggedBodyLength = 500;

    private const string RedactionMarker = "[REDACTED]";

    private static readonly TimeSpan MatchTimeout = TimeSpan.FromSeconds(1);

    /// <summary>OpenAI-shaped secret keys, wherever they appear in the body.</summary>
    private const string SecretKeyPattern = @"sk-[A-Za-z0-9\-_]{8,}";

    /// <summary>An echoed credential header, in either <c>Name: value</c> or <c>Name=value</c> form.</summary>
    private const string CredentialHeaderPattern = @"(?i)(Authorization|X-Auth-Token)\s*[:=]\s*\S+";

    /// <summary>
    /// Strips anything key-shaped out of <paramref name="body"/> and truncates the result to
    /// <see cref="MaximumLoggedBodyLength"/>.
    /// </summary>
    public static string RedactAndTruncate(string body)
    {
        var redacted = Regex.Replace(body, SecretKeyPattern, RedactionMarker, RegexOptions.None, MatchTimeout);
        redacted = Regex.Replace(
            redacted,
            CredentialHeaderPattern,
            $"$1={RedactionMarker}",
            RegexOptions.None,
            MatchTimeout);

        return redacted.Length > MaximumLoggedBodyLength
            ? redacted[..MaximumLoggedBodyLength] + "…"
            : redacted;
    }
}
