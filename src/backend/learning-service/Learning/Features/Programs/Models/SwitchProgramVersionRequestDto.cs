namespace Sellevate.Learning.Features.Programs.Models;

/// <summary>
/// Phase 40.17. The programme version the learner is agreeing to move to.
///
/// <para>
/// Naming the target is what makes the switch explicit rather than a "take me to whatever is newest"
/// button. The learner was shown a diff against a specific version; if the РОП publishes another one
/// in between, the id they send no longer matches the newest and the call is refused instead of
/// silently moving them onto a programme nobody showed them. That race is small and the refusal is
/// cheap — and the whole block exists because of what a silent programme change does to somebody on
/// lesson 8 of 21.
/// </para>
/// </summary>
public record SwitchProgramVersionRequestDto(Guid TargetProgramVersionId);
