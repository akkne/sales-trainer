using System.Text.Json;

namespace Sellevate.Learning.Features.Lessons.Services.Implementation;

/// <summary>
/// Phase 40.15. Writes an arbitrary JSON value in a canonical form, so that two documents which
/// mean the same thing produce the same bytes and therefore the same content hash.
///
/// <para>
/// Without this, <c>content_hash</c> does not do the job it exists for: an admin panel that
/// re-serializes an exercise's content with its object keys in a different order would look like a
/// content change on every save, and "publish with no changes" would mint a version every time —
/// which is precisely the metric-stepping failure 40.16 is meant to avoid.
/// </para>
///
/// <para>
/// The canonical form is: object properties sorted by ordinal (not culture-sensitive) comparison of
/// their names, array order preserved because array order is meaningful in exercise content, and
/// numbers written through unchanged. Numbers are the one deliberate gap: <c>1</c> and <c>1.0</c>
/// are the same number and different text, and normalizing them would mean choosing a numeric model
/// (double? decimal?) and silently rewriting customer content to fit it. Exercise content is
/// authored once and stored verbatim, so the textual form is stable in practice.
/// </para>
/// </summary>
public static class CanonicalJsonWriter
{
    public static void WriteCanonical(Utf8JsonWriter writer, JsonElement element)
    {
        ArgumentNullException.ThrowIfNull(writer);

        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var property in element.EnumerateObject().OrderBy(candidate => candidate.Name, StringComparer.Ordinal))
                {
                    writer.WritePropertyName(property.Name);
                    WriteCanonical(writer, property.Value);
                }

                writer.WriteEndObject();
                break;

            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in element.EnumerateArray())
                {
                    WriteCanonical(writer, item);
                }

                writer.WriteEndArray();
                break;

            case JsonValueKind.String:
                writer.WriteStringValue(element.GetString());
                break;

            case JsonValueKind.Number:
                writer.WriteRawValue(element.GetRawText(), skipInputValidation: true);
                break;

            case JsonValueKind.True:
                writer.WriteBooleanValue(true);
                break;

            case JsonValueKind.False:
                writer.WriteBooleanValue(false);
                break;

            default:
                writer.WriteNullValue();
                break;
        }
    }
}
