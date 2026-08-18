namespace Sellevate.Ai.Features.ContentGeneration.Models;

/// <summary>
/// Phase 40.27. What the pipeline extracted from the РОП's material, and the thing a human is asked
/// to confirm before a single exercise is generated (roadmap 40.27).
///
/// <para>
/// <b>Every field is optional and every collection may be empty.</b> The material a РОП pastes is
/// whatever they had — three slides, a call script, a page of notes — and a model that has to invent
/// an ICP because the contract says the field is required produces exactly the confident nonsense the
/// checkpoint exists to catch. An empty field is a question the review screen asks; a fabricated one
/// is a lie the review screen ratifies.
/// </para>
///
/// <para>
/// Phase 40.28 is what happens when the gaps add up: this record still describes only what was found,
/// and the judgement «этого мало, чтобы что-то генерировать» travels beside it in
/// <see cref="MaterialSufficiencyDto"/>. Keeping the two apart is what lets the extraction stay
/// honest — a model asked to both find nothing and justify finding nothing would start finding
/// things.
/// </para>
///
/// <para>
/// <b>The field list is the organization profile's field list</b> (product, ICP, tone, objections,
/// script stages, glossary, banned claims — docs/TENANCY/CONTENT_MODEL.md §3). That is not a
/// coincidence and it is not an invitation to write this row into the profile: see
/// docs/DECISIONS.md (2026-08-18) for why the checkpoint keeps its own draft.
/// </para>
/// </summary>
public sealed record ExtractedContentStructureDto(
    string? Product,
    string? Icp,
    string? Tone,
    IReadOnlyList<ExtractedObjectionDto> Objections,
    IReadOnlyList<string> ScriptStages,
    IReadOnlyDictionary<string, string> Glossary,
    IReadOnlyList<string> BannedClaims)
{
    public static ExtractedContentStructureDto Empty { get; } = new(
        Product: null,
        Icp: null,
        Tone: null,
        Objections: [],
        ScriptStages: [],
        Glossary: new Dictionary<string, string>(),
        BannedClaims: []);
}
