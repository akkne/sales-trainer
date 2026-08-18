using Sellevate.Learning.Features.ContentGeneration.Models;

namespace Sellevate.Learning.Infrastructure.Ai;

/// <summary>
/// Phase 40.27. Structuring: the raw material, plus whatever the organization has already confirmed
/// so the model fills gaps instead of arguing with a human.
/// </summary>
public sealed record AiStructureMaterialRequest(string Material, ContentStructureDto? KnownStructure);
