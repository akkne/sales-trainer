using System.Text;

namespace Sellevate.Learning.Features.Lessons.Services.Implementation;

/// <summary>
/// Phase 40.15. Produces and validates the value behind <c>UNIQUE (OrganizationId, Slug)</c>.
///
/// <para>
/// Deliberately does not transliterate the title. Lesson titles are Russian prose, and a
/// transliteration table is a long-lived guess about how «Работа с возражениями» should read in
/// latin that nobody asked for. The slug's job in this block is to be a stable identifier, not a
/// pretty URL — nothing routes by it yet — so an admin who wants a readable one supplies it, and
/// everyone else gets a collision-free machine slug derived from the row's own primary key. Making
/// them readable later is a rename, and a rename is safe precisely because nothing references the
/// slug.
/// </para>
/// </summary>
public static class LessonSlugGenerator
{
    public const int MaximumLength = 160;

    private const string GeneratedPrefix = "lesson-";

    /// <summary>Derived from the id, so it is unique by construction and never needs a retry loop.</summary>
    public static string GenerateFromLessonId(Guid lessonId)
        => GeneratedPrefix + lessonId.ToString("N");

    /// <summary>
    /// Accepts lowercase latin letters, digits and single hyphens. Anything else is refused rather
    /// than silently rewritten: an admin who typed a slug meant that slug, and quietly turning it
    /// into something else is how two lessons end up fighting over one identifier.
    /// </summary>
    public static bool TryNormalize(string? candidate, out string normalizedSlug)
    {
        normalizedSlug = string.Empty;

        if (string.IsNullOrWhiteSpace(candidate))
        {
            return false;
        }

        var trimmed = candidate.Trim().ToLowerInvariant();
        if (trimmed.Length > MaximumLength)
        {
            return false;
        }

        var builder = new StringBuilder(trimmed.Length);
        var previousWasHyphen = false;

        foreach (var character in trimmed)
        {
            var isAllowedCharacter = character is >= 'a' and <= 'z' or >= '0' and <= '9';
            if (isAllowedCharacter)
            {
                builder.Append(character);
                previousWasHyphen = false;
                continue;
            }

            if (character != '-' || previousWasHyphen || builder.Length == 0)
            {
                return false;
            }

            builder.Append(character);
            previousWasHyphen = true;
        }

        if (builder.Length == 0 || previousWasHyphen)
        {
            return false;
        }

        normalizedSlug = builder.ToString();
        return true;
    }
}
