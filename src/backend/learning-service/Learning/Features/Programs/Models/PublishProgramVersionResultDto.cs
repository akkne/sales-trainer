namespace Sellevate.Learning.Features.Programs.Models;

/// <summary>
/// Phase 40.17. <see cref="CreatedNewVersion"/> is <see langword="false"/> when the draft's items
/// were identical to the last published version's — the same guard 40.15's content hash gives
/// lessons, arrived at by comparing reference tuples because a programme has no body to hash.
///
/// <para>
/// It matters more here than it does for a lesson. A programme version that changed nothing would
/// still light up "a new version of your programme is available" for every enrolled learner and
/// then show them an empty diff, which teaches them to click through the notice without reading it
/// — and the notice is the entire mechanism by which a breaking change reaches a human.
/// </para>
/// </summary>
public record PublishProgramVersionResultDto(
    ProgramVersionDto Version,
    bool CreatedNewVersion
);
