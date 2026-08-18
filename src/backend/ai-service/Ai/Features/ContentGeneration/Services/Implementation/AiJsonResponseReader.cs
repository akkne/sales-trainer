using System.Text.Json;

namespace Sellevate.Ai.Features.ContentGeneration.Services.Implementation;

/// <summary>
/// Phase 40.27. Turns a completion into a <see cref="JsonDocument"/>, forgiving the one thing models
/// do anyway.
///
/// <para>
/// The fence-stripping is the same defence <c>ParseLogService</c> and the dialog reply parser
/// already carry: a model told "no markdown" wraps its answer in a <c>```json</c> fence often enough
/// that treating it as a provider failure would turn a perfectly good structure into a 503. It lives
/// in its own class here because two services in this feature need it, and copying it a third time
/// is how the three copies start disagreeing about the closing fence.
/// </para>
/// </summary>
internal static class AiJsonResponseReader
{
    /// <summary>
    /// Returns the parsed document, or <see langword="null"/> when the completion is not a JSON
    /// object. The caller decides what an unparseable answer means — for structuring it is a failed
    /// run, and never a silently empty structure.
    /// </summary>
    public static JsonDocument? TryReadObject(string completion)
    {
        if (string.IsNullOrWhiteSpace(completion))
        {
            return null;
        }

        var jsonText = StripMarkdownCodeFence(completion);

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(jsonText);
        }
        catch (JsonException)
        {
            return null;
        }

        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            document.Dispose();
            return null;
        }

        return document;
    }

    public static string? ReadStringOrNull(JsonElement parent, string propertyName)
    {
        if (!parent.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var value = property.GetString();
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    public static IReadOnlyList<string> ReadStringArray(JsonElement parent, string propertyName, int maximumCount)
    {
        if (!parent.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var values = new List<string>();
        foreach (var element in property.EnumerateArray())
        {
            if (values.Count >= maximumCount)
            {
                break;
            }

            if (element.ValueKind != JsonValueKind.String)
            {
                continue;
            }

            var value = element.GetString();
            if (!string.IsNullOrWhiteSpace(value))
            {
                values.Add(value.Trim());
            }
        }

        return values;
    }

    private static string StripMarkdownCodeFence(string text)
    {
        var trimmedText = text.Trim();
        if (!trimmedText.StartsWith("```", StringComparison.Ordinal))
        {
            return trimmedText;
        }

        var firstLineBreakIndex = trimmedText.IndexOf('\n');
        if (firstLineBreakIndex < 0)
        {
            return trimmedText;
        }

        var withoutOpeningFence = trimmedText[(firstLineBreakIndex + 1)..];
        var closingFenceIndex = withoutOpeningFence.LastIndexOf("```", StringComparison.Ordinal);

        return closingFenceIndex >= 0
            ? withoutOpeningFence[..closingFenceIndex].Trim()
            : withoutOpeningFence.Trim();
    }
}
