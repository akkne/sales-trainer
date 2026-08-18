using System.Text;
using System.Text.RegularExpressions;

namespace Sellevate.BuildingBlocks.ContentTemplating;

/// <summary>
/// Phase 40.19. Resolves <c>{{organization.*}}</c> placeholders out of an
/// <see cref="OrganizationProfileSnapshot"/> — the cheap half of customization, the one that lets a
/// single base lesson serve every customer instead of being forked per customer
/// (docs/TENANCY/CONTENT_MODEL.md §1 and §3).
///
/// <para>
/// <b>Rendering happens on read, never on write.</b> The template is what is stored — in
/// <c>Exercise.SerializedContent</c>, in a lesson title, in <c>DialogMode.ChatSystemPrompt</c> — and
/// therefore what 40.15 freezes into <c>LessonVersion.Content</c> and hashes into
/// <c>ContentHash</c>. Substituting before the write would give every organization a different hash
/// for the same base lesson, and the shared library would stop being shared: one version row per
/// customer per lesson, which is §1's fork with extra steps. So no code path in this class is
/// reachable from publishing, snapshotting or hashing, and that is a rule, not an accident.
/// </para>
///
/// <para>
/// <b>An unfilled field renders as neutral prose, not as a blank and not as the raw placeholder.</b>
/// A lesson that shows a salesperson <c>{{organization.icp}}</c> is worse than one that says
/// «ваш клиент» — the placeholder is a visible defect, the neutral wording is simply the sentence
/// the base lesson was written with before anybody filled the form in. Blanks are worse still:
/// «Расскажите, чем  помогает » reads as a bug in the product rather than as an empty profile.
/// See <see cref="Fallbacks"/>.
/// </para>
///
/// <para>
/// <b>Substitution is a single pass.</b> A value pulled out of the profile is inserted verbatim and
/// is never scanned again, so an administrator who types <c>{{organization.product}}</c> into their
/// own product field gets that text back rather than an expansion loop.
/// </para>
/// </summary>
public static class OrganizationPlaceholderRenderer
{
    /// <summary>
    /// Longest substituted value. The profile columns are unbounded <c>text</c>, and a placeholder
    /// can appear inside an AI system prompt, so without a ceiling one pasted-in product manual
    /// would push the actual lesson out of the model's context window. Truncation is marked with an
    /// ellipsis so a reader can tell a cut value from a short one.
    /// </summary>
    public const int MaximumSubstitutionLength = 2000;

    private const string Prefix = "organization.";
    private const string GlossaryPrefix = "glossary.";

    /// <summary>
    /// The whole content library is Russian prose, and <see cref="System.Text.Json"/>'s default
    /// encoder escapes every non-ASCII character as <c>\uXXXX</c> — six bytes for one letter, so a
    /// re-serialized exercise grows several times over for no gain. <c>UnicodeRanges.All</c> emits
    /// Cyrillic literally while still escaping <c>&lt;</c>, <c>&gt;</c>, <c>&amp;</c> and <c>'</c>,
    /// unlike <c>UnsafeRelaxedJsonEscaping</c>, which is the usual and wrong way to fix this.
    /// </summary>
    private static readonly System.Text.Json.JsonWriterOptions JsonWriterOptions = new()
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.Create(
            System.Text.Unicode.UnicodeRanges.All),
    };

    /// <summary>
    /// Ceiling on one match attempt. The pattern is linear and the input is stored content rather
    /// than request data, so this is a backstop against a pathological template, not a live hazard.
    /// </summary>
    private static readonly TimeSpan PlaceholderMatchTimeout = TimeSpan.FromSeconds(1);

    private static readonly Regex PlaceholderPattern = new(
        @"\{\{\s*(?<key>[A-Za-z0-9_.\-]+)\s*\}\}",
        RegexOptions.Compiled | RegexOptions.CultureInvariant,
        PlaceholderMatchTimeout);

    /// <summary>
    /// The neutral wording each supported placeholder falls back to. These are the phrases the base
    /// library is written in, so a lesson with no profile behind it reads exactly as it did before
    /// 40.19 existed.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string> Fallbacks = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["product"] = "ваш продукт",
        ["icp"] = "ваш клиент",
        ["tone"] = "нейтральный деловой",
        ["objections"] = "типичные возражения ваших клиентов",
        ["script"] = "ваш скрипт звонка",
    };

    /// <summary>
    /// Renders <paramref name="template"/> against <paramref name="profile"/>.
    ///
    /// <para>
    /// <paramref name="unresolvedKeys"/> collects placeholders this renderer does not know at all —
    /// a typo like <c>{{organization.produkt}}</c>, or a <c>{{organization.glossary.crm}}</c> whose
    /// term the customer has not defined. They are removed from the output rather than left visible,
    /// and the caller logs them: silently vanishing text is a content bug somebody has to be able to
    /// find, but showing curly braces to a salesperson is not the way to report it.
    /// </para>
    ///
    /// <para>
    /// Placeholders outside the <c>organization.</c> namespace are left <b>untouched</b>. The
    /// seeded hidden dialog modes complete their prompts from placeholders the code supplies at run
    /// time (docs/TENANCY/CONTENT_MODEL.md §4), and eating those would break company-call practice.
    /// </para>
    /// </summary>
    public static string Render(
        string? template,
        OrganizationProfileSnapshot? profile,
        ICollection<string>? unresolvedKeys = null)
    {
        if (string.IsNullOrEmpty(template) || !template.Contains("{{", StringComparison.Ordinal))
        {
            return template ?? string.Empty;
        }

        var resolved = profile ?? OrganizationProfileSnapshot.Empty;

        return PlaceholderPattern.Replace(template, match =>
        {
            var key = match.Groups["key"].Value;

            if (!key.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
            {
                return match.Value;
            }

            var field = key[Prefix.Length..];
            var substitution = Resolve(field, resolved);

            if (substitution is null)
            {
                unresolvedKeys?.Add(key);
                return string.Empty;
            }

            return Truncate(substitution);
        });
    }

    /// <summary>
    /// True when <paramref name="template"/> contains at least one <c>{{organization.*}}</c>
    /// placeholder. Lets a caller skip the work — and skip loading the profile at all — for the
    /// overwhelming majority of content, which has no placeholders in it.
    /// </summary>
    public static bool HasOrganizationPlaceholders(string? template)
        => !string.IsNullOrEmpty(template)
           && template.Contains("{{", StringComparison.Ordinal)
           && PlaceholderPattern.Matches(template).Any(match =>
               match.Groups["key"].Value.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Resolves one <c>organization.</c>-namespaced field to its substitution, or
    /// <see langword="null"/> when the field is not a placeholder this renderer knows.
    ///
    /// <para>
    /// A glossary miss falls back to the term itself, not to nothing: the sentence still reads, it
    /// just uses the generic word instead of the customer's word for it. A known-but-unfilled field
    /// falls back to <see cref="Fallbacks"/>.
    /// </para>
    /// </summary>
    private static string? Resolve(string field, OrganizationProfileSnapshot profile)
    {
        if (field.StartsWith(GlossaryPrefix, StringComparison.OrdinalIgnoreCase))
        {
            var term = field[GlossaryPrefix.Length..];
            if (string.IsNullOrWhiteSpace(term))
            {
                return null;
            }

            var match = profile.Glossary.FirstOrDefault(entry =>
                string.Equals(entry.Key, term, StringComparison.OrdinalIgnoreCase));

            return string.IsNullOrWhiteSpace(match.Value) ? term : match.Value;
        }

        var value = field.ToLowerInvariant() switch
        {
            "product" => profile.Product,
            "icp" => profile.Icp,
            "tone" => profile.Tone,
            "objections" => JoinObjections(profile),
            "script" => profile.ScriptStages.Count == 0 ? null : string.Join(" → ", profile.ScriptStages),
            _ => null,
        };

        if (!string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        return Fallbacks.TryGetValue(field, out var fallback) ? fallback : null;
    }

    private static string? JoinObjections(OrganizationProfileSnapshot profile)
    {
        var texts = profile.Objections
            .Select(objection => objection.Text)
            .Where(text => !string.IsNullOrWhiteSpace(text))
            .ToList();

        return texts.Count == 0 ? null : string.Join("; ", texts);
    }

    private static string Truncate(string value)
    {
        if (value.Length <= MaximumSubstitutionLength)
        {
            return value;
        }

        return string.Concat(value.AsSpan(0, MaximumSubstitutionLength), "…");
    }

    /// <summary>
    /// Renders every string leaf of a JSON document in place, leaving structure, numbers and
    /// booleans alone. This is how exercise content gets parameterized: the answer key, the option
    /// order and the exercise type are not text a customer writes, and re-serializing them through a
    /// text renderer would be a way to corrupt them for no gain.
    /// </summary>
    public static string RenderJsonStrings(
        string json,
        OrganizationProfileSnapshot? profile,
        ICollection<string>? unresolvedKeys = null)
    {
        ArgumentNullException.ThrowIfNull(json);

        if (!json.Contains("{{", StringComparison.Ordinal))
        {
            return json;
        }

        using var document = System.Text.Json.JsonDocument.Parse(json);
        var buffer = new MemoryStream();
        using (var writer = new System.Text.Json.Utf8JsonWriter(buffer, JsonWriterOptions))
        {
            WriteRendered(document.RootElement, writer, profile, unresolvedKeys, propertyName: null);
        }

        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    private static void WriteRendered(
        System.Text.Json.JsonElement element,
        System.Text.Json.Utf8JsonWriter writer,
        OrganizationProfileSnapshot? profile,
        ICollection<string>? unresolvedKeys,
        string? propertyName)
    {
        if (propertyName is not null)
        {
            writer.WritePropertyName(propertyName);
        }

        switch (element.ValueKind)
        {
            case System.Text.Json.JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var property in element.EnumerateObject())
                {
                    WriteRendered(property.Value, writer, profile, unresolvedKeys, property.Name);
                }
                writer.WriteEndObject();
                break;

            case System.Text.Json.JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in element.EnumerateArray())
                {
                    WriteRendered(item, writer, profile, unresolvedKeys, propertyName: null);
                }
                writer.WriteEndArray();
                break;

            case System.Text.Json.JsonValueKind.String:
                writer.WriteStringValue(Render(element.GetString(), profile, unresolvedKeys));
                break;

            default:
                element.WriteTo(writer);
                break;
        }
    }
}
