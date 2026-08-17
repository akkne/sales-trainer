using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Sellevate.Learning.Features.Lessons.Models;

namespace Sellevate.Learning.Features.Lessons.Services.Implementation;

/// <summary>
/// Phase 40.15. Turns a lesson and its ordered exercises into the canonical JSON document stored in
/// <c>LessonVersion.Content</c>, and hashes it.
///
/// <para>
/// Shape of the document (every object's keys in ordinal order, which is what makes the hash
/// reproducible — see <see cref="CanonicalJsonWriter"/>):
/// </para>
/// <code>
/// {
///   "exercises": [
///     { "content": {...}, "customAiPrompt": null, "exerciseId": "...", "orderInLesson": 1, "type": "choose_option" }
///   ],
///   "schemaVersion": 1,
///   "title": "..."
/// }
/// </code>
///
/// <para>
/// <c>exerciseId</c> is inside the snapshot and therefore inside the hash. That is the identity
/// 40.16 needs to say which exercise inside which version an attempt answered, and the cost is
/// accepted knowingly: deleting an exercise row and recreating it with identical content produces a
/// new hash and so a new version. That is the honest answer — the identity really did change.
/// </para>
///
/// <para>
/// The column is <c>jsonb</c>, so Postgres re-normalizes the document on write and a
/// <c>SELECT</c> does not return these bytes. The hash is defined over the form produced here,
/// never over what the database hands back.
/// </para>
/// </summary>
public static class LessonSnapshotSerializer
{
    /// <summary>
    /// Bumped only when the shape below changes in a way a reader has to know about. Stored inside
    /// the snapshot so a 40.16/40.17 reader can branch on it instead of guessing from the keys.
    /// </summary>
    public const int CurrentSchemaVersion = 1;

    private static readonly JsonWriterOptions CanonicalWriterOptions = new()
    {
        Indented = false,
        // Relaxed escaping keeps Russian lesson text readable in the stored document instead of
        // turning every letter into \uXXXX. Both sides of a hash comparison go through this same
        // options object, so the choice cannot make two equal snapshots hash differently.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static string BuildCanonicalContent(Lesson lesson, IReadOnlyList<Exercise> orderedExercises)
    {
        ArgumentNullException.ThrowIfNull(lesson);
        ArgumentNullException.ThrowIfNull(orderedExercises);

        using var buffer = new MemoryStream();

        using (var writer = new Utf8JsonWriter(buffer, CanonicalWriterOptions))
        {
            writer.WriteStartObject();

            writer.WritePropertyName("exercises");
            writer.WriteStartArray();
            foreach (var exercise in orderedExercises)
            {
                WriteExercise(writer, exercise);
            }

            writer.WriteEndArray();

            writer.WriteNumber("schemaVersion", CurrentSchemaVersion);
            writer.WriteString("title", lesson.Title);

            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    /// <summary>
    /// Phase 40.22. The exercise ids frozen inside a snapshot, in the order they were written.
    ///
    /// <para>
    /// This is what makes "точность по упражнениям ≥80%" answerable: an assignment pins a
    /// <c>LessonVersion</c>, and the set of exercises the threshold is measured over is the set the
    /// snapshot names — not whatever the mutable <c>Exercises</c> table holds today. Reading it here
    /// rather than re-querying by <c>LessonId</c> is the difference between grading somebody against
    /// what they were asked to do and grading them against what the lesson has since become, which
    /// is the defect 40.16 spent a block removing from progress.
    /// </para>
    ///
    /// <para>
    /// Tolerant by design, like every other read of a stored document in this service: a snapshot
    /// this reader cannot parse yields an empty set, which the evaluator treats as "cannot be
    /// judged" and never as "passed".
    /// </para>
    /// </summary>
    public static IReadOnlyList<Guid> ReadExerciseIds(string? canonicalContent)
    {
        if (string.IsNullOrWhiteSpace(canonicalContent))
        {
            return [];
        }

        try
        {
            using var document = JsonDocument.Parse(canonicalContent);

            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty("exercises", out var exercises)
                || exercises.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            var exerciseIds = new List<Guid>(exercises.GetArrayLength());

            foreach (var exercise in exercises.EnumerateArray())
            {
                if (exercise.ValueKind == JsonValueKind.Object
                    && exercise.TryGetProperty("exerciseId", out var exerciseId)
                    && exerciseId.ValueKind == JsonValueKind.String
                    && Guid.TryParse(exerciseId.GetString(), out var parsedExerciseId))
                {
                    exerciseIds.Add(parsedExerciseId);
                }
            }

            return exerciseIds;
        }
        catch (JsonException)
        {
            return [];
        }
    }

    /// <summary>Lowercase hex SHA-256 of the UTF-8 bytes of <paramref name="canonicalContent"/>.</summary>
    public static string ComputeContentHash(string canonicalContent)
    {
        ArgumentNullException.ThrowIfNull(canonicalContent);

        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(canonicalContent));
        return Convert.ToHexStringLower(hashBytes);
    }

    private static void WriteExercise(Utf8JsonWriter writer, Exercise exercise)
    {
        writer.WriteStartObject();

        writer.WritePropertyName("content");
        using (var contentDocument = ParseExerciseContent(exercise))
        {
            CanonicalJsonWriter.WriteCanonical(writer, contentDocument.RootElement);
        }

        if (exercise.CustomAiPrompt is null)
        {
            writer.WriteNull("customAiPrompt");
        }
        else
        {
            writer.WriteString("customAiPrompt", exercise.CustomAiPrompt);
        }

        writer.WriteString("exerciseId", exercise.Id);
        writer.WriteNumber("orderInLesson", exercise.OrderInLesson);
        writer.WriteString("type", exercise.Type);

        writer.WriteEndObject();
    }

    /// <summary>
    /// Fails loudly rather than snapshotting a placeholder. A lesson version is what the dashboard
    /// will grade historical attempts against; freezing "{}" in place of unparseable content would
    /// make every one of those attempts meaningless, quietly and permanently.
    /// </summary>
    private static JsonDocument ParseExerciseContent(Exercise exercise)
    {
        try
        {
            return JsonDocument.Parse(string.IsNullOrWhiteSpace(exercise.SerializedContent)
                ? "{}"
                : exercise.SerializedContent);
        }
        catch (JsonException parseFailure)
        {
            throw new InvalidOperationException(
                $"Exercise {exercise.Id} in lesson {exercise.LessonId} has content that is not valid JSON, "
                + "so the lesson cannot be snapshotted. Fix the exercise before publishing.",
                parseFailure);
        }
    }
}
