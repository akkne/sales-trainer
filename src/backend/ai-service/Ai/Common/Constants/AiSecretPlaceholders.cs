namespace Sellevate.Ai.Common.Constants;

/// <summary>
/// Recognises a provider key that is a placeholder rather than a secret, so a deployment that never
/// had one injected degrades to "the feature is off" instead of authenticating with a stand-in.
///
/// <para>
/// <b>Known gap, deliberately preserved.</b> This recognises the <c>REPLACE…</c> family only, while
/// <c>appsettings.json</c> marks an un-injected secret as <c>INJECTED_FROM_ENV</c>. A deployment
/// missing the environment variable therefore reads as configured and sends the marker string to the
/// provider as a key, producing a stream of 401s rather than a clean feature-off state. Widening the
/// check would change behaviour and is recorded in <c>docs/DONT_FORGET.md</c> rather than fixed here.
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
    /// <see langword="true"/> when <paramref name="secret"/> looks like a usable credential —
    /// non-blank and not a placeholder.
    /// </summary>
    public static bool IsRealSecret(string? secret)
        => !string.IsNullOrWhiteSpace(secret)
           && !secret.StartsWith(ReplacePrefix, StringComparison.OrdinalIgnoreCase);
}
