using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Sellevate.Ai.Features.Dialog.Models;

namespace Sellevate.Ai.Features.Dialog.Overrides;

/// <summary>
/// Phase 40.18. The canonical form of a dialog mode's authored content, and its fingerprint.
///
/// <para>
/// This is what makes "has the base prompt moved since we forked it?" answerable without a version
/// table. Identity (<c>Id</c>, <c>BundleId</c>), ownership (<c>OrganizationId</c>,
/// <c>ParentModeId</c>) and bookkeeping (<c>CreatedAt</c>, <c>UpdatedAt</c>) are excluded: a base
/// re-saved unchanged has not changed, and marking every customer's override stale for that is how
/// a review queue teaches the person reading it to click through without looking.
/// </para>
/// </summary>
public static class DialogModeSnapshot
{
    public const int CurrentSchemaVersion = 1;

    private static readonly JsonWriterOptions CanonicalWriterOptions = new()
    {
        Indented = false,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static string BuildCanonicalContent(DialogMode mode)
    {
        ArgumentNullException.ThrowIfNull(mode);

        using var buffer = new MemoryStream();

        using (var writer = new Utf8JsonWriter(buffer, CanonicalWriterOptions))
        {
            writer.WriteStartObject();

            writer.WriteString("chatSystemPrompt", mode.ChatSystemPrompt);
            writer.WriteString("description", mode.Description);
            writer.WriteString("feedbackSystemPrompt", mode.FeedbackSystemPrompt);
            writer.WriteBoolean("isActive", mode.IsActive);
            writer.WriteString("key", mode.Key);
            writer.WriteNumber("schemaVersion", CurrentSchemaVersion);
            writer.WriteNumber("sortOrder", mode.SortOrder);
            writer.WriteString("title", mode.Title);
            writer.WriteBoolean("voiceEnabled", mode.VoiceEnabled);

            if (mode.VoiceId is null)
            {
                writer.WriteNull("voiceId");
            }
            else
            {
                writer.WriteString("voiceId", mode.VoiceId);
            }

            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    /// <summary>Lowercase hex SHA-256, the same convention learning-service uses for lesson versions.</summary>
    public static string ComputeContentHash(string canonicalContent)
    {
        ArgumentNullException.ThrowIfNull(canonicalContent);

        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonicalContent)));
    }

    public static string ComputeContentHash(DialogMode mode)
        => ComputeContentHash(BuildCanonicalContent(mode));
}
