using System.Text.Json;
using Sellevate.Learning.Features.ContentGeneration.Models;

namespace Sellevate.Learning.Features.ContentGeneration.Services.Implementation;

/// <summary>
/// Phase 40.27. Between <see cref="ContentStructureDto"/> and the <c>jsonb</c> column that holds it.
///
/// <para>
/// <b>Reading is total.</b> A malformed or half-written structure column comes back as
/// <see cref="ContentStructureDto.Empty"/> rather than throwing, the same call
/// <c>OrganizationProfileSnapshot.FromJson</c> makes: the caller is an administrator opening a review
/// screen, and failing the whole page to protect one document would hide the run they are trying to
/// fix. What must never happen is a *silent* empty in the other direction — the worker refuses to
/// store a structure it could not parse, so an empty here always means an empty there.
/// </para>
///
/// <para>
/// <b>Writing bounds every collection.</b> The reviewer's edit arrives from a browser and this is the
/// only place that decides how big a structure may get; without it a paste into the objections list
/// is an unbounded <c>jsonb</c> document that later has to fit in a prompt.
/// </para>
/// </summary>
internal static class ContentStructureDocumentSerializer
{
    /// <summary>Matches ai-service's per-value cap, which matches the render path's (docs/AI_SERVICE.md).</summary>
    public const int MaximumValueLength = 2000;

    public const int MaximumObjectionCount = 10;
    public const int MaximumScriptStageCount = 12;
    public const int MaximumGlossaryTermCount = 30;
    public const int MaximumBannedClaimCount = 20;

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public static string Serialize(ContentStructureDto structure)
    {
        ArgumentNullException.ThrowIfNull(structure);

        return JsonSerializer.Serialize(Normalize(structure), SerializerOptions);
    }

    public static ContentStructureDto Deserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return ContentStructureDto.Empty;
        }

        try
        {
            return Normalize(JsonSerializer.Deserialize<ContentStructureDto>(json, SerializerOptions)
                             ?? ContentStructureDto.Empty);
        }
        catch (JsonException)
        {
            return ContentStructureDto.Empty;
        }
    }

    /// <summary>
    /// Trims, drops blanks, caps every collection and bounds every value. Applied on the way in and
    /// on the way out, so a row written before a cap changed still reads within the current one.
    /// </summary>
    public static ContentStructureDto Normalize(ContentStructureDto structure)
    {
        ArgumentNullException.ThrowIfNull(structure);

        var glossary = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in structure.Glossary ?? new Dictionary<string, string>())
        {
            if (glossary.Count >= MaximumGlossaryTermCount)
            {
                break;
            }

            var term = entry.Key?.Trim();
            var definition = Bound(entry.Value);
            if (!string.IsNullOrEmpty(term) && definition is not null)
            {
                glossary[term] = definition;
            }
        }

        return new ContentStructureDto(
            Bound(structure.Product),
            Bound(structure.Icp),
            Bound(structure.Tone),
            (structure.Objections ?? [])
                .Select(objection => new ContentStructureObjectionDto(
                    Bound(objection.Text) ?? string.Empty,
                    Bound(objection.BestResponse)))
                .Where(objection => objection.Text.Length > 0)
                .Take(MaximumObjectionCount)
                .ToList(),
            BoundAll(structure.ScriptStages, MaximumScriptStageCount),
            glossary,
            BoundAll(structure.BannedClaims, MaximumBannedClaimCount));
    }

    private static string? Bound(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= MaximumValueLength ? trimmed : trimmed[..MaximumValueLength];
    }

    private static IReadOnlyList<string> BoundAll(IReadOnlyList<string>? values, int maximumCount)
        => (values ?? [])
            .Select(Bound)
            .Where(value => value is not null)
            .Select(value => value!)
            .Take(maximumCount)
            .ToList();
}
