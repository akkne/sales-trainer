namespace Sellevate.Ai.Features.ContentGeneration.Models;

/// <summary>
/// Phase 40.28. What <c>POST /ai/content/structure</c> returns: the structure that was read out of
/// the material, and whether there was enough material to read.
///
/// <para>
/// <b>Why the verdict travels with the structure rather than in its own endpoint.</b> The two are
/// answers to the same reading of the same text; splitting them would mean paying for that reading
/// twice and would allow the pair to disagree — a "sufficient" verdict beside an empty structure, or
/// the reverse. 40.27 returned the structure alone, which is why this is a shape change rather than
/// an added field: the only caller is learning-service's <c>AiContentPipelineClient</c>, and both
/// ends of the wire ship together.
/// </para>
/// </summary>
public sealed record StructuredMaterialDto(
    ExtractedContentStructureDto Structure,
    MaterialSufficiencyDto Sufficiency);
