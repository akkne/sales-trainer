using System.Text.Json;
using Sellevate.Learning.Common.Constants;
using Sellevate.Learning.Features.ContentGeneration.Models;

namespace Sellevate.Learning.Features.ContentGeneration.Services.Implementation;

/// <summary>
/// Phase 40.28. Between <see cref="ContentInsufficiencyDto"/> and the <c>jsonb</c> column that holds
/// it — the same two rules <c>ContentStructureDocumentSerializer</c> follows, for the same reasons.
///
/// <para>
/// <b>Reading is total and writing is strict.</b> A malformed refusal column reads back as
/// <see langword="null"/> rather than throwing, because failing an administrator's whole list to
/// protect one document hides the run they came to fix. Writing drops unknown codes and re-resolves
/// nothing: what is stored is what the customer was told.
/// </para>
/// </summary>
internal static class ContentInsufficiencyDocumentSerializer
{
    /// <summary>
    /// One per known code, and there are seven of them. A cap rather than a coincidence: the document
    /// is written from a code list and a duplicated code would otherwise repeat a sentence.
    /// </summary>
    public const int MaximumGapCount = 12;

    /// <summary>The model's diagnostic note is a sentence, and only ever read by a developer.</summary>
    public const int MaximumNoteLength = 500;

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public static string Serialize(ContentInsufficiencyDto insufficiency)
    {
        ArgumentNullException.ThrowIfNull(insufficiency);

        return JsonSerializer.Serialize(Normalize(insufficiency), SerializerOptions);
    }

    public static ContentInsufficiencyDto? Deserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            var insufficiency = JsonSerializer.Deserialize<ContentInsufficiencyDto>(json, SerializerOptions);

            return insufficiency is null ? null : Normalize(insufficiency);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static ContentInsufficiencyDto Normalize(ContentInsufficiencyDto insufficiency)
    {
        var gaps = (insufficiency.Gaps ?? [])
            .Where(gap => ContentSufficiencyCodes.IsKnown(gap.Code))
            .DistinctBy(gap => gap.Code, StringComparer.Ordinal)
            .Take(MaximumGapCount)
            .ToList();

        var note = insufficiency.Note?.Trim();
        if (note is { Length: > MaximumNoteLength })
        {
            note = note[..MaximumNoteLength];
        }

        return new ContentInsufficiencyDto(
            insufficiency.Stage == ContentInsufficiencyDto.MaterialStage
                ? ContentInsufficiencyDto.MaterialStage
                : ContentInsufficiencyDto.StructureStage,
            gaps,
            string.IsNullOrWhiteSpace(note) ? null : note);
    }
}
