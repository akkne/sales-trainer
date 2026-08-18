using Sellevate.BuildingBlocks.Tenancy;

namespace Sellevate.Learning.Features.Programs.Models;

/// <summary>
/// Phase 40.17. Which programme snapshot one learner is pinned to
/// (docs/TENANCY/CONTENT_MODEL.md §2.5).
///
/// <para>
/// <b>One row per user per organization, and only the learner may move it.</b> An administrator
/// enrolling people puts newcomers on the newest published version and leaves everybody who already
/// has a row exactly where they are; the switch to a newer version is a separate call the learner
/// makes for themselves, after being shown the diff. That asymmetry is the block: a manager on
/// lesson 8 of 21 must never find the programme rearranged underneath them, and "never" has to mean
/// there is no code path that does it, not that nobody currently presses the button.
/// </para>
///
/// <para>
/// Not to be confused with the older sense of "enrollment" in this service — a
/// <c>UserSkillProgress</c> row means the learner opted into a skill in the live tree
/// (<c>SkillTreeService</c>). That is a preference over the live library; this is a pin to a frozen
/// programme. They answer different questions and neither replaces the other.
/// </para>
/// </summary>
public sealed class ProgramEnrollment : ITenantScoped
{
    public Guid Id { get; set; }

    /// <summary>
    /// Owning organization; never null. The security boundary is the Postgres row-level-security
    /// policy created by the AddProgramVersioning migration.
    /// </summary>
    public Guid OrganizationId { get; set; }

    public Guid UserId { get; set; }

    public Guid ProgramVersionId { get; set; }

    /// <summary>
    /// Where the learner was before their last explicit switch, or null if they have never
    /// switched. Kept as one column rather than a history table because the question it answers —
    /// "did this person's programme move, and from what" — is asked about the last move only; a
    /// full audit trail of curriculum moves is a reporting feature nobody has asked for and would
    /// be a second table to keep in step with the pin.
    /// </summary>
    public Guid? PreviousProgramVersionId { get; set; }

    public DateTime EnrolledAt { get; set; }

    public DateTime? SwitchedAt { get; set; }

    public ProgramVersion? ProgramVersion { get; set; }
}
