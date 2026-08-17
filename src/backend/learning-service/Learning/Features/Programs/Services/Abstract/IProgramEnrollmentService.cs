using Sellevate.Learning.Features.Programs.Models;

namespace Sellevate.Learning.Features.Programs.Services.Abstract;

/// <summary>
/// Phase 40.17. Who is pinned to which programme snapshot, and the one way a pin may move
/// (docs/TENANCY/CONTENT_MODEL.md §2.5).
///
/// <para>
/// The split between <see cref="EnrollAsync"/> and <see cref="SwitchAsync"/> is the block. An
/// administrator may put newcomers on the newest published version and may not move anybody who
/// already has a pin; a learner may move their own pin, after being shown what changes. There is
/// deliberately no third operation that moves somebody else's pin, because "a manager on lesson 8 of
/// 21 must not find the programme rearranged" is a claim about which code paths exist, not about
/// which buttons are currently drawn.
/// </para>
/// </summary>
public interface IProgramEnrollmentService
{
    /// <summary>
    /// The caller's own programme: the snapshot they are pinned to, its items in order, and — when a
    /// newer published version exists — the diff switching to it would apply. Never returns
    /// <see langword="null"/>: not being enrolled is an answer, not an error.
    /// </summary>
    Task<MyProgramDto> GetMyProgramAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProgramEnrollmentDto>> GetEnrollmentsAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Pins a learner who has no pin yet to the newest published programme version. Idempotent, and
    /// deliberately so: a learner who already has a pin comes back unchanged rather than being moved
    /// onto the new version. Returns <see langword="null"/> when the organization has published no
    /// programme version at all.
    /// </summary>
    Task<ProgramEnrollmentDto?> EnrollAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Moves the learner's own pin to <paramref name="targetProgramVersionId"/>. Refuses — returning
    /// <see langword="null"/> — when the learner has no pin, when the target is not a published
    /// version of their organization, or when it is the version they are already on. The target is
    /// named rather than implied so that a version published between showing the diff and accepting
    /// it cannot be the one the learner lands on.
    /// </summary>
    Task<MyProgramDto?> SwitchAsync(
        Guid userId,
        Guid targetProgramVersionId,
        CancellationToken cancellationToken = default);
}
