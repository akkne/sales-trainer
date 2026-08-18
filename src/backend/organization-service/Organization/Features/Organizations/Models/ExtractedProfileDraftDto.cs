namespace Sellevate.Organization.Features.Organizations.Models;

/// <summary>
/// Phase 40.29. What was read out of the РОП's material, on its way into the profile.
///
/// <para>
/// <b>This is learning-service's <c>ContentStructureDto</c>, field for field</b>, and that is a
/// decision 40.27 made on this block's behalf: the extracted structure was deliberately given the
/// profile's shape so that promoting it would be a copy rather than a translation
/// (docs/DECISIONS.md, 2026-08-18). The type is redeclared here rather than shared, the same way
/// <c>MaterialGapCodes</c> and <c>ContentSufficiencyCodes</c> are redeclared on both sides of the
/// ai-service wire: this is a request body of organization-service's public API and it must not move
/// when a record in another service's assembly does.
/// </para>
///
/// <para>
/// <b>There is no <c>jobId</c> here, and that is the block's cross-service decision.</b> The draft
/// arrives in the request body, from a caller who has just read it off a pipeline run they own.
/// organization-service therefore stays the only writer of the profile, learning-service never writes
/// another service's aggregate, and the read-only replica stays read-only. It also adds no authority:
/// the same administrator can already <c>PUT</c> an arbitrary structure onto a run and an arbitrary
/// profile onto this row, so carrying a document between two routes they may already write is not a
/// new trust boundary. Full reasoning and the three rejected alternatives in docs/DECISIONS.md.
/// </para>
///
/// <para>
/// Every field is optional, because a gap is the thing this whole block is built to notice. A draft
/// that arrives with nothing in it merges to nothing and the interview asks all seven questions.
/// </para>
/// </summary>
public sealed record ExtractedProfileDraftDto(
    string? Product,
    string? Icp,
    string? Tone,
    IReadOnlyList<ExtractedProfileObjectionDto>? Objections,
    IReadOnlyList<string>? ScriptStages,
    IReadOnlyDictionary<string, string>? Glossary,
    IReadOnlyList<string>? BannedClaims);
