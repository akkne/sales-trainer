namespace Sellevate.Learning.Features.ContentGeneration.Models;

/// <summary>
/// Phase 40.28. The answer to a refusal: «вот ещё материал».
/// </summary>
/// <param name="Material">
/// What the РОП is adding — the call script the deck did not have, the objections list, the
/// transcript. It is <b>appended</b> to the run's material rather than replacing it: the original is
/// what the extracted structure came from, and a run whose stated source no longer contains what was
/// read out of it cannot answer «откуда это взялось» — the second question a reviewer asks at the
/// checkpoint.
/// </param>
public sealed record SupplementContentMaterialRequestDto(string Material);
