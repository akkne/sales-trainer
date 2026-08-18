using Sellevate.Ai.Features.ContentGeneration.Models;

namespace Sellevate.Ai.Features.ContentGeneration.Services.Abstract;

/// <summary>
/// Phase 40.27. The first half of the admin content pipeline: read the РОП's material and say what
/// is in it, without generating anything.
///
/// <para>
/// Phase 40.28 added the second half of the same answer — whether there was enough material to be
/// worth generating from. It comes back in the same completion because the model forms it while
/// reading anyway, so the threshold costs no extra call (docs/DECISIONS.md, 2026-08-18).
/// </para>
/// </summary>
public interface IMaterialStructuringService
{
    Task<StructuredMaterialDto> ExtractAsync(
        StructureMaterialRequestDto request,
        CancellationToken cancellationToken = default);
}
