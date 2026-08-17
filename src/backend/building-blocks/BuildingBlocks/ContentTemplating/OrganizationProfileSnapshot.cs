using System.Text.Json;

namespace Sellevate.BuildingBlocks.ContentTemplating;

/// <summary>One objection the customer's reps actually hear, with the answer their РОП wants given.</summary>
public sealed record OrganizationObjectionSnapshot(string Text, string? BestResponse);

/// <summary>
/// Phase 40.19. A read-only, already-parsed view of one organization's
/// <c>organization_profile</c> row (docs/TENANCY/CONTENT_MODEL.md §3), in the shape the render
/// path wants rather than the shape the table stores.
///
/// <para>
/// <b>Why this lives in BuildingBlocks and not in organization-service.</b> Two other services —
/// learning and ai — resolve placeholders out of this profile, and both keep their own replica of
/// it (docs/TENANCY/BACKGROUND_JOBS.md). If each parsed the four jsonb columns its own way, the
/// same base lesson would render differently in a lesson and in a persona prompt, which is exactly
/// the drift the one-base-lesson-for-everyone design exists to prevent.
/// </para>
///
/// <para>
/// Every collection is non-null and every scalar is nullable. A profile nobody has filled in is a
/// perfectly ordinary value here (<see cref="Empty"/>), not an error and not a missing object:
/// rendering has to work on day one of a trial, before the РОП has opened the form.
/// </para>
/// </summary>
public sealed record OrganizationProfileSnapshot(
    string? Product,
    string? Icp,
    string? Tone,
    IReadOnlyList<OrganizationObjectionSnapshot> Objections,
    IReadOnlyList<string> ScriptStages,
    IReadOnlyDictionary<string, string> Glossary,
    IReadOnlyList<string> BannedClaims)
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// The profile of an organization that has not filled the form in — and also what a caller with
    /// no tenant at all gets. Rendering against it yields the neutral wording the base lesson was
    /// written with (see <see cref="OrganizationPlaceholderRenderer"/>), never a blank.
    /// </summary>
    public static OrganizationProfileSnapshot Empty { get; } = new(
        Product: null,
        Icp: null,
        Tone: null,
        Objections: [],
        ScriptStages: [],
        Glossary: new Dictionary<string, string>(),
        BannedClaims: []);

    /// <summary>
    /// Builds a snapshot from the four jsonb columns as they are stored.
    ///
    /// <para>
    /// Malformed JSON degrades to the empty collection for that one field instead of throwing. The
    /// caller is a learner opening a lesson or a persona answering a message: failing the whole read
    /// because somebody's glossary column got corrupted would take the product down to protect a
    /// substitution, and the neutral fallback is a correct lesson either way.
    /// </para>
    /// </summary>
    public static OrganizationProfileSnapshot FromJson(
        string? product,
        string? icp,
        string? tone,
        string? objectionsJson,
        string? scriptJson,
        string? glossaryJson,
        string? bannedClaimsJson)
        => new(
            product,
            icp,
            tone,
            Deserialize<List<OrganizationObjectionSnapshot>>(objectionsJson) ?? [],
            (Deserialize<List<string>>(scriptJson) ?? []).Where(stage => !string.IsNullOrWhiteSpace(stage)).ToList(),
            Deserialize<Dictionary<string, string>>(glossaryJson) ?? new Dictionary<string, string>(),
            (Deserialize<List<string>>(bannedClaimsJson) ?? []).Where(claim => !string.IsNullOrWhiteSpace(claim)).ToList());

    private static T? Deserialize<T>(string? json)
        where T : class
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<T>(json, SerializerOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
