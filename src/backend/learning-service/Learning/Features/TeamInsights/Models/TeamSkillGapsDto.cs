namespace Sellevate.Learning.Features.TeamInsights.Models;

/// <summary>
/// Phase 40.31. What the dashboard proposes doing next (docs/TENANCY/ASSIGNMENTS.md §3.4).
///
/// <para>
/// <b>Two lists, because the second one is what keeps the first one credible.</b>
/// <see cref="Gaps"/> is what there is a button for. <see cref="Suppressed"/> is every stage that
/// currently qualifies as a failure and is deliberately not being offered — dismissed, already
/// being worked on, or worked on so recently that the number has not had time to move. A panel that
/// merely showed nothing would be indistinguishable from a broken one.
/// </para>
///
/// <para>
/// <b>Every threshold is echoed back.</b> Same call 40.25 made with
/// <c>MinimumAttemptsForAccuracy</c>: a screen that has to explain why the reddest cell on the heat
/// map produced no suggestion needs the numbers that decided it, and hard-coding them a second time
/// in the client is how the two eventually disagree.
/// </para>
/// </summary>
/// <param name="WindowStart">
/// The start of the window the judgement was made over — the same window
/// <c>GET /admin/team/skill-map</c> drew, because the suggestion is derived from that same call and
/// not from a second aggregation. A red cell with no suggestion, or a suggestion for a cell that is
/// not red, would both be bugs the screen could not explain.
/// </param>
/// <param name="RosterKnown">
/// Whether identity-service could be asked who works here. False does not withhold a suggestion —
/// the accuracy numbers are true either way — but it does mean the "how many managers are struggling"
/// counts were taken over whoever has practised rather than over the team.
/// </param>
public sealed record TeamSkillGapsDto(
    DateTime WindowStart,
    int MinimumAttemptsForGap,
    int MaximumAccuracyPercentForGap,
    int MinimumStrugglingManagers,
    IReadOnlyList<TeamSkillGapDto> Gaps,
    IReadOnlyList<SuppressedTeamSkillGapDto> Suppressed,
    bool RosterKnown);

/// <summary>
/// Phase 40.31. One failing stage of the sales funnel, with the sentence that goes on the button.
/// </summary>
/// <param name="SourceRef">
/// The string an assignment born from this gap will carry as its <c>source_ref</c> —
/// <c>skill-gap:&lt;stage&gt;@&lt;date&gt;</c>. Returned rather than left to the client to assemble,
/// so the provenance of a <c>gap_detected</c> assignment is written by the code that measured the
/// gap and never by the caller that asked for one.
/// </param>
/// <param name="StrugglingManagerCount">
/// How many managers have a reportable cell on this stage at or below the threshold. This is the
/// difference between «провал команды» and one person's bad week, and it is the reason a single
/// weak manager never triggers a generation run: that is a conversation, and 40.25 already names
/// the person for it.
/// </param>
/// <param name="ProposedTitle">The run's title, so the button and the resulting run agree.</param>
/// <param name="ProposedGoal">
/// The measurement as one Russian sentence. It becomes the assignment's goal, which is where the
/// numbers behind a <c>gap_detected</c> assignment stay readable a year later — the reference itself
/// carries only the coordinates.
/// </param>
public sealed record TeamSkillGapDto(
    string StageKey,
    string StageLabel,
    string SourceRef,
    int AttemptCount,
    int AccuracyPercent,
    int StrugglingManagerCount,
    int MeasuredManagerCount,
    IReadOnlyList<TeamSkillGapSkillDto> WeakestSkills,
    string ProposedTitle,
    string ProposedGoal);

/// <summary>
/// Phase 40.31. One of the skills inside a failing stage that the team is worst at, so the material
/// the button composes names the actual weakness rather than the whole stage.
/// </summary>
public sealed record TeamSkillGapSkillDto(
    Guid SkillId,
    string Title,
    int AttemptCount,
    int AccuracyPercent);

/// <summary>
/// Phase 40.31. A stage that qualifies as a failure and is not being offered, with the reason and
/// the date the reason runs out.
/// </summary>
/// <param name="Reason">One of <c>TeamSkillGapSuppressionReasons</c>.</param>
/// <param name="SuppressedUntil">
/// When the suggestion comes back on its own, or null when that depends on something other than the
/// calendar — a run somebody has to finish looking at.
/// </param>
/// <param name="ContentGenerationJobId">
/// The run that is holding this gap, when there is one. It is what turns «почему мне ничего не
/// предлагают» into a link.
/// </param>
public sealed record SuppressedTeamSkillGapDto(
    string StageKey,
    string StageLabel,
    int AttemptCount,
    int AccuracyPercent,
    string Reason,
    DateTime? SuppressedUntil,
    Guid? ContentGenerationJobId);
