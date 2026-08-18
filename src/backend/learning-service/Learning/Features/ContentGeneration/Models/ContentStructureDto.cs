using Sellevate.BuildingBlocks.ContentTemplating;

namespace Sellevate.Learning.Features.ContentGeneration.Models;

/// <summary>
/// Phase 40.27. The artifact at the checkpoint: what the pipeline read out of the РОП's material,
/// shown back to them as «всё верно? что убрать, что добавить?» before anything is generated.
///
/// <para>
/// <b>It is field-for-field the organization profile of docs/TENANCY/CONTENT_MODEL.md §3, and it is
/// deliberately not the profile row.</b> The identical shape is what makes roadmap 40.29 — the
/// profile filled in by interview rather than by a thirty-field form — a promotion of this document
/// instead of a second extraction pipeline. Keeping it a separate draft is what stops one uploaded
/// deck from silently overwriting <c>banned_claims</c> a compliance officer entered, and it is what
/// keeps generation reading a structure a human confirmed rather than one the profile happened to
/// hold. Full reasoning and the rejected alternative in docs/DECISIONS.md (2026-08-18).
/// </para>
///
/// <para>
/// Every field is optional. A gap is a question the review screen asks; a filled-in gap the model
/// invented is a fabrication the review screen would ratify.
/// </para>
/// </summary>
public sealed record ContentStructureDto(
    string? Product,
    string? Icp,
    string? Tone,
    IReadOnlyList<ContentStructureObjectionDto> Objections,
    IReadOnlyList<string> ScriptStages,
    IReadOnlyDictionary<string, string> Glossary,
    IReadOnlyList<string> BannedClaims)
{
    public static ContentStructureDto Empty { get; } = new(
        Product: null,
        Icp: null,
        Tone: null,
        Objections: [],
        ScriptStages: [],
        Glossary: new Dictionary<string, string>(),
        BannedClaims: []);

    /// <summary>
    /// Seeds a run from what the organization already told us, so a customer who filled the profile
    /// in is not asked the same seven questions again — and so the model is asked to fill gaps rather
    /// than to contradict a human.
    /// </summary>
    public static ContentStructureDto FromProfile(OrganizationProfileSnapshot profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        return new ContentStructureDto(
            profile.Product,
            profile.Icp,
            profile.Tone,
            profile.Objections
                .Select(objection => new ContentStructureObjectionDto(objection.Text, objection.BestResponse))
                .ToList(),
            profile.ScriptStages.ToList(),
            new Dictionary<string, string>(profile.Glossary, StringComparer.OrdinalIgnoreCase),
            profile.BannedClaims.ToList());
    }

    /// <summary>
    /// True when there is nothing in here at all. Used to decide whether a structure is worth sending
    /// to the model as a seed — an empty one is sent as nothing rather than as an object of nulls.
    ///
    /// <para>
    /// It is <b>not</b> the sufficiency threshold. «Есть хоть что-то» and «хватит ли этого на четыре
    /// хороших упражнения» are different questions, and 40.28 answers the second one in
    /// <c>ContentSufficiencyInspector</c>, which is what approval consults.
    /// </para>
    /// </summary>
    public bool IsEmpty
        => string.IsNullOrWhiteSpace(Product)
           && string.IsNullOrWhiteSpace(Icp)
           && Objections.Count == 0
           && ScriptStages.Count == 0;
}
