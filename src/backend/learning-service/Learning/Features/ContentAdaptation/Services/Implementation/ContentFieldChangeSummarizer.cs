using System.Text.Json;
using Sellevate.Learning.Features.ContentAdaptation.Models;

namespace Sellevate.Learning.Features.ContentAdaptation.Services.Implementation;

/// <summary>
/// Phase 40.32. Which leaves of an exercise body differ between what it says now and what the model
/// proposes — the machine-readable half of «дифф».
///
/// <para>
/// <b>What this is not.</b> It is not a merge and it is not a patch. 40.18 ruled out combining two
/// versions of prose and grading criteria on the grounds that the result is plausible nonsense that
/// then grades a living salesperson, and that ruling stands here: nothing on the server produces a
/// third document. This walks two trees in parallel and reports where they disagree, which is a
/// comparison and not an authoring act — the two full documents travel beside it and the screen can
/// always fall back to showing them whole.
/// </para>
///
/// <para>
/// <b>Why leaves and not objects.</b> «content изменился» is true of every proposal and therefore
/// says nothing. «options[1].text» and «explanation» is a list a person can scan in three seconds
/// and is the difference between reviewing sixty items and clicking through them. Arrays are compared
/// positionally on purpose: a rewrite that reorders options is a change the reviewer should see, not
/// one to be smoothed away by matching on content.
/// </para>
/// </summary>
internal static class ContentFieldChangeSummarizer
{
    /// <summary>
    /// A proposal that rewrote thirty leaves is not a tone adjustment, and listing all of them helps
    /// nobody. Past this the list is truncated and the screen falls back to the two documents.
    /// </summary>
    public const int MaximumChangeCount = 40;

    /// <summary>Long enough to recognise a sentence, short enough that a list of forty stays a list.</summary>
    public const int MaximumValueLength = 400;

    private const int MaximumDepth = 12;

    public static IReadOnlyList<ContentFieldChangeDto> Compare(JsonElement before, JsonElement after)
    {
        var changes = new List<ContentFieldChangeDto>();
        Walk(string.Empty, before, after, changes, 0);

        return changes;
    }

    /// <summary>
    /// Compares two stored documents. Either being unparseable yields an empty list rather than an
    /// exception — the same tolerance every other reader of a stored document in this service shows,
    /// because a queue that refuses to render is worse than one whose change list is missing.
    /// </summary>
    public static IReadOnlyList<ContentFieldChangeDto> Compare(string? beforeJson, string? afterJson)
    {
        if (string.IsNullOrWhiteSpace(beforeJson) || string.IsNullOrWhiteSpace(afterJson))
        {
            return [];
        }

        try
        {
            using var beforeDocument = JsonDocument.Parse(beforeJson);
            using var afterDocument = JsonDocument.Parse(afterJson);

            return Compare(beforeDocument.RootElement, afterDocument.RootElement);
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static void Walk(
        string path,
        JsonElement before,
        JsonElement after,
        List<ContentFieldChangeDto> changes,
        int depth)
    {
        if (changes.Count >= MaximumChangeCount)
        {
            return;
        }

        if (depth >= MaximumDepth || before.ValueKind != after.ValueKind)
        {
            AddIfDifferent(path, before, after, changes);
            return;
        }

        switch (before.ValueKind)
        {
            case JsonValueKind.Object:
                WalkObject(path, before, after, changes, depth);
                return;

            case JsonValueKind.Array:
                WalkArray(path, before, after, changes, depth);
                return;

            default:
                AddIfDifferent(path, before, after, changes);
                return;
        }
    }

    /// <summary>
    /// Walks both objects' property names in <b>ordinal</b> order, so the same pair of documents always
    /// produces the same change list. A list whose order depended on how Postgres normalized the
    /// <c>jsonb</c> would read to a reviewer as churn rather than as a diff.
    /// </summary>
    private static void WalkObject(
        string path,
        JsonElement before,
        JsonElement after,
        List<ContentFieldChangeDto> changes,
        int depth)
    {
        var names = before.EnumerateObject().Select(property => property.Name)
            .Concat(after.EnumerateObject().Select(property => property.Name))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal);

        foreach (var name in names)
        {
            if (changes.Count >= MaximumChangeCount)
            {
                return;
            }

            var hasBefore = before.TryGetProperty(name, out var beforeValue);
            var hasAfter = after.TryGetProperty(name, out var afterValue);
            var childPath = string.IsNullOrEmpty(path) ? name : $"{path}.{name}";

            if (hasBefore && hasAfter)
            {
                Walk(childPath, beforeValue, afterValue, changes, depth + 1);
                continue;
            }

            changes.Add(new ContentFieldChangeDto(
                childPath,
                hasBefore ? Render(beforeValue) : null,
                hasAfter ? Render(afterValue) : null));
        }
    }

    private static void WalkArray(
        string path,
        JsonElement before,
        JsonElement after,
        List<ContentFieldChangeDto> changes,
        int depth)
    {
        var beforeItems = before.EnumerateArray().ToArray();
        var afterItems = after.EnumerateArray().ToArray();
        var length = Math.Max(beforeItems.Length, afterItems.Length);

        for (var index = 0; index < length; index++)
        {
            if (changes.Count >= MaximumChangeCount)
            {
                return;
            }

            var childPath = $"{path}[{index}]";

            if (index >= beforeItems.Length)
            {
                changes.Add(new ContentFieldChangeDto(childPath, null, Render(afterItems[index])));
                continue;
            }

            if (index >= afterItems.Length)
            {
                changes.Add(new ContentFieldChangeDto(childPath, Render(beforeItems[index]), null));
                continue;
            }

            Walk(childPath, beforeItems[index], afterItems[index], changes, depth + 1);
        }
    }

    private static void AddIfDifferent(
        string path,
        JsonElement before,
        JsonElement after,
        List<ContentFieldChangeDto> changes)
    {
        var beforeText = Render(before);
        var afterText = Render(after);

        if (string.Equals(beforeText, afterText, StringComparison.Ordinal))
        {
            return;
        }

        changes.Add(new ContentFieldChangeDto(
            string.IsNullOrEmpty(path) ? "content" : path,
            beforeText,
            afterText));
    }

    private static string Render(JsonElement element)
    {
        var text = element.ValueKind switch
        {
            JsonValueKind.String => element.GetString() ?? string.Empty,
            JsonValueKind.Null or JsonValueKind.Undefined => string.Empty,
            _ => element.GetRawText()
        };

        return text.Length <= MaximumValueLength ? text : text[..MaximumValueLength];
    }
}
