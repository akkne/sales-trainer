using Sellevate.Learning.Features.ContentGeneration.Models;

namespace Sellevate.Learning.Infrastructure.Ai;

/// <summary>
/// Phase 40.28. What came back from <c>POST /ai/content/structure</c>: the structure, and whether
/// there was enough material to make one worth having. Two answers to the same reading of the same
/// text, which is why they travel together rather than in two calls.
///
/// <para>
/// Both halves are nullable because both come off the wire: a deserialiser will happily hand back a
/// record with null fields when the other side omits them, and the caller has to decide what a
/// missing half means rather than dereference it. A missing structure is an empty one; a missing
/// verdict is no opinion, which the structure check outlives anyway.
/// </para>
/// </summary>
public sealed record AiStructuredMaterial(
    ContentStructureDto? Structure,
    AiMaterialSufficiency? Sufficiency);
