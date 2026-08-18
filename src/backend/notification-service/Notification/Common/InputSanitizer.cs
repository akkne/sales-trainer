namespace Sellevate.Notification.Common;

/// <summary>
/// Sanitizes untrusted string fields before they are persisted as notification content.
/// Stored values are plain text — the frontend MUST render them as text (not inner HTML)
/// to prevent injection. Control characters are stripped here as a defence-in-depth measure.
/// </summary>
internal static class InputSanitizer
{
    /// <summary>
    /// Removes ASCII and C1 control characters (U+0000–U+001F, U+007F, U+0080–U+009F) and the
    /// zero-width / bidirectional formatting characters from <paramref name="value"/>. Printable
    /// text is left intact, and a value that contains none of them is returned unallocated.
    /// </summary>
    public static string StripControlCharacters(string value)
    {
        if (string.IsNullOrEmpty(value))
            return value;

        var hasControl = false;
        foreach (var character in value)
        {
            if (IsControlOrZeroWidth(character))
            {
                hasControl = true;
                break;
            }
        }

        if (!hasControl)
            return value;

        var buffer = new System.Text.StringBuilder(value.Length);
        foreach (var character in value)
        {
            if (!IsControlOrZeroWidth(character))
                buffer.Append(character);
        }

        return buffer.ToString();
    }

    /// <summary>
    /// Validates that <paramref name="url"/> is a relative app path (starts with '/') or null.
    /// Rejects absolute URLs and non-'/' schemes — <c>javascript:</c>, <c>data:</c>, a host of
    /// somebody else's choosing — to prevent open-redirect and injection through an action link.
    /// Returns the original value when valid, or null when rejected.
    /// </summary>
    public static string? SanitizeActionUrl(string? url)
    {
        if (url is null)
            return null;

        if (url.StartsWith('/'))
            return url;

        return null;
    }

    /// <summary>
    /// The character classes stripped by <see cref="StripControlCharacters"/>: the ASCII control
    /// range, DEL, the C1 range, then the zero-width space / non-joiner / joiner, the
    /// left-to-right and right-to-left marks, and the zero-width no-break space (BOM).
    /// </summary>
    private static bool IsControlOrZeroWidth(char character) =>
        character < 0x0020 ||
        character == 0x007F ||
        (character >= 0x0080 && character <= 0x009F) ||
        character == '​' ||
        character == '‌' ||
        character == '‍' ||
        character == '‎' ||
        character == '‏' ||
        character == '﻿';
}
