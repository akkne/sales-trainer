using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Sellevate.Learning.Features.Lessons.Services.Implementation;
using Sellevate.Learning.Features.Reference.Models;
using Sellevate.Learning.Features.Techniques.Models;

namespace Sellevate.Learning.Features.Content;

/// <summary>
/// Phase 40.18. Canonical serialization of the two content families that have no version table —
/// <see cref="Technique"/> and <see cref="ReferenceMaterial"/> — so that "has the base moved since
/// we forked?" has an answer.
///
/// <para>
/// A lesson answers that question by comparing version ids: 40.15 froze every published lesson into
/// an immutable row, so the fork point is a pointer at a snapshot that still exists. These two
/// families have no such table, and building two more was not this block's job. The substitute is a
/// fingerprint: the override records the hash of the base at fork time, and the base is stale when
/// its current hash differs. What that gives up is showing the upstream change as a before/after —
/// the previous base text is simply not stored anywhere. What it does not give up is the staleness
/// signal itself, which is the thing the review queue is built on (docs/DECISIONS.md, 2026-08-18).
/// </para>
///
/// <para>
/// What goes into the hash is the authored content and nothing else. Identity (<c>Id</c>), ownership
/// (<c>OrganizationId</c>, <c>ParentTechniqueId</c>) and bookkeeping (<c>CreatedAt</c>,
/// <c>UpdatedAt</c>) are excluded on purpose: a base whose <c>UpdatedAt</c> moved because somebody
/// re-saved it unchanged has not changed, and marking every customer's override stale for that is
/// exactly the false alarm that teaches a РОП to click through the queue without reading it.
/// </para>
/// </summary>
public static class ContentSnapshotSerializer
{
    /// <summary>Bumped only when the shape below changes in a way that must invalidate stored hashes.</summary>
    public const int CurrentSchemaVersion = 1;

    private static readonly JsonWriterOptions CanonicalWriterOptions = new()
    {
        Indented = false,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>
    /// The technique together with the two rows that are part of it rather than beside it: its
    /// coach (1:1, and carrying prose the learner reads) and its additional skill links, sorted so
    /// that the set and not the insertion order is what is hashed.
    /// </summary>
    public static string BuildCanonicalContent(
        Technique technique,
        TechniqueCoach? coach,
        IReadOnlyList<Guid> additionalSkillIds)
    {
        ArgumentNullException.ThrowIfNull(technique);
        ArgumentNullException.ThrowIfNull(additionalSkillIds);

        using var buffer = new MemoryStream();

        using (var writer = new Utf8JsonWriter(buffer, CanonicalWriterOptions))
        {
            writer.WriteStartObject();

            writer.WritePropertyName("additionalSkillIds");
            writer.WriteStartArray();
            foreach (var skillId in additionalSkillIds.Order())
            {
                writer.WriteStringValue(skillId);
            }

            writer.WriteEndArray();

            WriteJsonOrNull(writer, "caseJson", technique.CaseJson);

            writer.WritePropertyName("coach");
            if (coach is null)
            {
                writer.WriteNullValue();
            }
            else
            {
                writer.WriteStartObject();
                writer.WriteString("avatarSeed", coach.AvatarSeed);
                WriteJsonOrNull(writer, "challengesJson", coach.ChallengesJson);
                writer.WriteString("name", coach.Name);
                writer.WriteString("quote", coach.Quote);
                writer.WriteString("role", coach.Role);
                writer.WriteEndObject();
            }

            writer.WriteNumber("difficulty", technique.Difficulty);
            WriteJsonOrNull(writer, "dialogJson", technique.DialogJson);
            writer.WriteString("name", technique.Name);

            if (technique.PrimarySkillId is { } primarySkillId)
            {
                writer.WriteString("primarySkillId", primarySkillId);
            }
            else
            {
                writer.WriteNull("primarySkillId");
            }

            writer.WriteNumber("schemaVersion", CurrentSchemaVersion);
            writer.WriteString("slug", technique.Slug);
            writer.WriteNumber("sortOrder", technique.SortOrder);
            writer.WriteString("summary", technique.Summary);

            writer.WritePropertyName("tags");
            writer.WriteStartArray();
            foreach (var tag in technique.Tags)
            {
                writer.WriteStringValue(tag);
            }

            writer.WriteEndArray();

            writer.WriteString("body", technique.Body);

            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    public static string BuildCanonicalContent(ReferenceMaterial material)
    {
        ArgumentNullException.ThrowIfNull(material);

        using var buffer = new MemoryStream();

        using (var writer = new Utf8JsonWriter(buffer, CanonicalWriterOptions))
        {
            writer.WriteStartObject();

            if (material.Category is null)
            {
                writer.WriteNull("category");
            }
            else
            {
                writer.WriteString("category", material.Category);
            }

            writer.WriteString("markdownContent", material.MarkdownContent);
            writer.WriteNumber("schemaVersion", CurrentSchemaVersion);
            writer.WriteString("skillId", material.SkillId);
            writer.WriteNumber("sortOrder", material.SortOrder);

            if (material.Tags is null)
            {
                writer.WriteNull("tags");
            }
            else
            {
                writer.WriteString("tags", material.Tags);
            }

            writer.WriteString("title", material.Title);

            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    /// <summary>Lowercase hex SHA-256, the same convention <c>LessonVersion.ContentHash</c> uses.</summary>
    public static string ComputeContentHash(string canonicalContent)
        => LessonSnapshotSerializer.ComputeContentHash(canonicalContent);

    /// <summary>
    /// Writes a stored <c>jsonb</c> column through <see cref="CanonicalJsonWriter"/> rather than as
    /// a string. Postgres re-normalizes jsonb on write, so the text that comes back has whatever key
    /// order the server chose; hashing it verbatim would make the same document fingerprint
    /// differently on two servers and mark every override stale for no reason.
    /// </summary>
    private static void WriteJsonOrNull(Utf8JsonWriter writer, string propertyName, string? json)
    {
        writer.WritePropertyName(propertyName);

        if (string.IsNullOrWhiteSpace(json))
        {
            writer.WriteNullValue();
            return;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            CanonicalJsonWriter.WriteCanonical(writer, document.RootElement);
        }
        catch (JsonException)
        {
            // The column is jsonb, so this is unreachable through the database — but the in-memory
            // provider the unit tests use enforces no such thing, and a fingerprint that throws is
            // worse than one that treats unparseable text as itself.
            writer.WriteStringValue(json);
        }
    }
}
