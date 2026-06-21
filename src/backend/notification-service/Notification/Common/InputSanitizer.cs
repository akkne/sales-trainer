namespace Sellevate.Notification.Common;

/// <summary>
/// Sanitizes untrusted string fields before they are persisted as notification content.
/// Stored values are plain text — the frontend MUST render them as text (not inner HTML)
/// to prevent injection. Control characters are stripped here as a defence-in-depth measure.
/// </summary>
internal static class InputSanitizer
{
    /// <summary>
    /// Removes ASCII control characters (U+0000–U+001F, U+007F) and Unicode category
    /// "Control" / zero-width characters from <paramref name="value"/>.
    /// Printable text is left intact.
    /// </summary>
    public static string StripControlCharacters(string value)
    {
        if (string.IsNullOrEmpty(value))
            return value;

        // Fast path: no control characters at all.
        var hasControl = false;
        foreach (var ch in value)
        {
            if (IsControlOrZeroWidth(ch))
            {
                hasControl = true;
                break;
            }
        }

        if (!hasControl)
            return value;

        var buffer = new System.Text.StringBuilder(value.Length);
        foreach (var ch in value)
        {
            if (!IsControlOrZeroWidth(ch))
                buffer.Append(ch);
        }

        return buffer.ToString();
    }

    /// <summary>
    /// Validates that <paramref name="url"/> is a relative app path (starts with '/') or null.
    /// Rejects absolute URLs and non-'/' schemes to prevent open-redirect / injection via ActionUrl.
    /// Returns the original value when valid, or null when rejected.
    /// </summary>
    public static string? SanitizeActionUrl(string? url)
    {
        if (url is null)
            return null;

        // Must be a relative path starting with '/'
        if (url.StartsWith('/'))
            return url;

        // Reject anything else (absolute URLs, javascript:, data:, etc.)
        return null;
    }

    private static bool IsControlOrZeroWidth(char ch) =>
        // ASCII control range
        ch < 0x0020 ||
        ch == 0x007F ||
        // C1 controls
        (ch >= 0x0080 && ch <= 0x009F) ||
        // Zero-width / formatting characters
        ch == '​' || // zero-width space
        ch == '‌' || // zero-width non-joiner
        ch == '‍' || // zero-width joiner
        ch == '‎' || // left-to-right mark
        ch == '‏' || // right-to-left mark
        ch == '﻿';   // zero-width no-break space (BOM)
}
