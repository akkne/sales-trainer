namespace Sellevate.Ai.Features.ContentGeneration.Models;

/// <summary>
/// Phase 40.27. The first half of the pipeline: raw material in, structure out, nothing generated.
/// </summary>
/// <param name="Material">
/// Whatever the РОП pasted — a product deck's text, a call script, notes from a training session. It
/// is fenced as data in the prompt, never as instructions, the same defence every human-authored
/// text in this service has carried since 39.17.
/// </param>
/// <param name="KnownStructure">
/// What the organization has already told us, so the model is asked to fill gaps rather than to
/// re-derive an answer somebody already gave. Null when the caller has nothing. Passing it is what
/// makes a second run on the same organization cheap instead of contradictory.
/// </param>
public sealed record StructureMaterialRequestDto(
    string Material,
    ExtractedContentStructureDto? KnownStructure = null);
