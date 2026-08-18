using System.Text.Json;
using Sellevate.Learning.Common.Constants;
using Sellevate.Learning.Features.ContentAdaptation.Models;

namespace Sellevate.Learning.Features.ContentAdaptation.Services.Implementation;

/// <summary>
/// Phase 40.32. Between the review findings and the <c>jsonb</c> column that holds them — the same
/// two rules <c>ContentInsufficiencyDocumentSerializer</c> follows, for the same reasons.
///
/// <para>
/// <b>Reading is total and writing is strict.</b> A malformed findings column reads back as an empty
/// list rather than throwing, because failing an administrator's whole queue to protect one row
/// hides the fifty-nine items they came to review. Writing drops codes this service does not know,
/// so a code a model invented can never reach a customer as an empty bullet.
/// </para>
///
/// <para>
/// <b>Only the code and the quoted fragment are stored.</b> The sentence and the severity are looked
/// up on read, so improving the wording of a finding improves it retroactively everywhere — and so
/// that a stored document can never disagree with <see cref="ContentReviewFindingCodes"/> about what
/// counts as blocking.
/// </para>
/// </summary>
internal static class ContentReviewFindingDocumentSerializer
{
    /// <summary>
    /// One per known code, so seven. A cap rather than a coincidence: the same code twice on one
    /// exercise would repeat a sentence, and a review that repeats itself reads as noise.
    /// </summary>
    public const int MaximumFindingCount = 7;

    /// <summary>The quoted fragment is a phrase from the exercise, not an essay about it.</summary>
    public const int MaximumDetailLength = 300;

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public static string Serialize(IReadOnlyList<StoredContentReviewFinding> findings)
    {
        ArgumentNullException.ThrowIfNull(findings);

        return JsonSerializer.Serialize(Normalize(findings), SerializerOptions);
    }

    /// <summary>
    /// Keeps only codes this service knows, drops duplicates, and trims the quoted fragment. Applied
    /// on the way in and again on the way out, so a document written by an older build cannot smuggle
    /// a retired code into a screen.
    /// </summary>
    public static IReadOnlyList<StoredContentReviewFinding> Normalize(
        IReadOnlyList<StoredContentReviewFinding> findings)
    {
        ArgumentNullException.ThrowIfNull(findings);

        return findings
            .Where(finding => ContentReviewFindingCodes.IsKnown(finding.Code))
            .DistinctBy(finding => finding.Code, StringComparer.Ordinal)
            .Take(MaximumFindingCount)
            .Select(finding => finding with { Detail = TrimDetail(finding.Detail) })
            .ToList();
    }

    public static IReadOnlyList<StoredContentReviewFinding> Deserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            var findings = JsonSerializer.Deserialize<List<StoredContentReviewFinding>>(json, SerializerOptions);

            return findings is null ? [] : Normalize(findings);
        }
        catch (JsonException)
        {
            return [];
        }
    }

    /// <summary>Resolves stored codes into the sentences and severities the screen shows.</summary>
    public static IReadOnlyList<ContentReviewFindingDto> Describe(string? json)
        => Deserialize(json)
            .Select(finding => new ContentReviewFindingDto(
                finding.Code,
                ContentReviewFindingCodes.SeverityFor(finding.Code),
                ContentReviewFindingCodes.MessageFor(finding.Code) ?? string.Empty,
                finding.Detail))
            .ToList();

    public static bool HasBlockingFinding(string? json)
        => Deserialize(json).Any(finding =>
            ContentReviewFindingCodes.SeverityFor(finding.Code) == ContentReviewFindingCodes.BlockingSeverity);

    private static string? TrimDetail(string? detail)
    {
        var trimmed = detail?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }

        return trimmed.Length <= MaximumDetailLength ? trimmed : trimmed[..MaximumDetailLength];
    }
}
