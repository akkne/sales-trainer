namespace Sellevate.Learning.Features.TeamInsights.Models;

/// <summary>
/// Phase 40.25. The two team-level answers of docs/TENANCY/ASSIGNMENTS.md §4 in one object: the
/// skill heat map, and — for each manager — where exactly they are weakest, named by the stage of
/// the sales funnel that skill belongs to.
///
/// <para>
/// <b>One object because they are one query.</b> "Per manager: where they fail, mapped to the
/// funnel stage" and "per team: a skill heat map" are the same matrix read along its two axes.
/// Splitting them into two endpoints would run the same aggregation twice and let the two screens
/// disagree about the same window.
/// </para>
///
/// <para>
/// <b>The stage vocabulary is the platform's existing one and not a second one.</b> <c>Skill.Stage</c>
/// has named a sales-funnel stage since long before this phase, with <c>SkillStages</c> as its
/// lookup (label, accent, order). Inventing a parallel "funnel stage" concept here would give the
/// product two answers to "which stage is this about" and no rule for which one the РОП is looking
/// at — see docs/DECISIONS.md (2026-08-18).
/// </para>
/// </summary>
/// <param name="WindowStart">
/// Attempts before this instant are not counted. Team readiness is a statement about now: a manager
/// who was weak on closing in March and has practised since is not weak on closing.
/// </param>
/// <param name="Stages">Every stage that appears on a skill, in the lookup's own order.</param>
/// <param name="Skills">The heat map's columns, each with the team-wide number for that skill.</param>
/// <param name="Members">The heat map's rows.</param>
/// <param name="UnattributedAttemptCount">
/// Attempts inside the window whose exercise no longer exists, so no skill can be named for them.
/// Reported in its own bucket rather than folded anywhere, the same call
/// docs/ANALYTICS_SERVICE.md makes for <c>unversionedAttempts</c>: folding an unknown into a known
/// bucket is a claim nobody can check.
/// </param>
/// <param name="MinimumAttemptsForAccuracy">
/// How many attempts a cell needs before it reports a percentage at all. Echoed back so the screen
/// can explain a blank cell instead of drawing it as a zero.
/// </param>
/// <param name="RosterKnown">
/// Whether identity-service could be asked who works here. False means <see cref="Members"/> was
/// derived from whoever has practised rather than from the team, so somebody who has done nothing
/// at all is missing from it — which is precisely the person the screen most wants to show.
/// </param>
public sealed record TeamSkillMapDto(
    DateTime WindowStart,
    IReadOnlyList<TeamSkillMapStageDto> Stages,
    IReadOnlyList<TeamSkillMapSkillDto> Skills,
    IReadOnlyList<TeamSkillMapMemberDto> Members,
    int UnattributedAttemptCount,
    int MinimumAttemptsForAccuracy,
    bool RosterKnown);

/// <summary>Phase 40.25. One stage of the sales funnel, as the skill tree already names it.</summary>
public sealed record TeamSkillMapStageDto(
    string Key,
    string Label,
    string Accent,
    int Order,
    int AttemptCount,
    int? AccuracyPercent);

/// <summary>Phase 40.25. One column of the heat map.</summary>
public sealed record TeamSkillMapSkillDto(
    Guid SkillId,
    string Title,
    string StageKey,
    int OrderInTree,
    int AttemptCount,
    int? AccuracyPercent);

/// <summary>
/// Phase 40.25. One row of the heat map — one manager.
///
/// <para>
/// <see cref="WeakestStageKey"/> and <see cref="WeakestSkillId"/> are the roadmap's "where exactly
/// do they sag" computed server-side rather than left to the screen, so that every consumer of this
/// endpoint answers it the same way: the lowest-scoring cell that has enough attempts to report a
/// number at all. Both are null for somebody with no cell that qualifies — which is a different
/// statement from "they are weak everywhere" and must not be drawn as one.
/// </para>
/// </summary>
public sealed record TeamSkillMapMemberDto(
    Guid UserId,
    string? DisplayName,
    bool? IsActiveMember,
    int AttemptCount,
    int? AccuracyPercent,
    string? WeakestStageKey,
    Guid? WeakestSkillId,
    int DialogCount,
    int? DialogAverageScore,
    IReadOnlyList<TeamSkillMapCellDto> Stages,
    IReadOnlyList<TeamSkillMapCellDto> Skills);

/// <summary>
/// Phase 40.25. One cell: how much practice, and how much of it was right.
///
/// <para>
/// <see cref="AccuracyPercent"/> is null — not zero — below
/// <c>MinimumAttemptsForAccuracy</c>. Two answers out of two is 100% and one out of two is 50%, and
/// neither is a fact about anybody; a heat map that paints those cells is a heat map that sends the
/// РОП to coach the wrong person. Same argument 40.22 made for withholding an accuracy until every
/// exercise in a set has been attempted.
/// </para>
/// </summary>
public sealed record TeamSkillMapCellDto(
    string Key,
    int AttemptCount,
    int? AccuracyPercent);
