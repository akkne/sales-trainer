namespace Sellevate.Learning.Features.Assignments.Models;

/// <summary>
/// Phase 40.22. One assignment's completion rule, parsed out of the <c>completion_rule</c> jsonb
/// column into the three numbers every kind reduces to.
///
/// <para>
/// The reduction is what keeps <c>AssignmentProgressRecords</c> readable with two columns. Whatever
/// the kind, the question the РОП's screen asks is the same: how many times did this person try
/// (<c>AttemptCount</c>), and how close did they get (<c>BestScore</c>, 0–100). A rule therefore has
/// to say what one attempt is and what bar an attempt clears — that is
/// <see cref="Threshold"/> — and how many cleared attempts the assignment wants, which is
/// <see cref="RequiredCount"/>.
/// </para>
/// </summary>
/// <param name="Kind">One of <see cref="Common.Constants.AssignmentCompletionRuleKinds"/>.</param>
/// <param name="Threshold">
/// The 0–100 bar one attempt must reach. Always at least 1: a bar of zero is "no threshold", which
/// is the failure mode docs/TENANCY/ASSIGNMENTS.md §1.1 exists to keep out of the schema.
/// </param>
/// <param name="RequiredCount">
/// How many attempts must reach the bar. Always at least 1, and always 1 for a rule whose
/// measurement is a single aggregate over the whole set rather than a per-attempt score.
/// </param>
internal sealed record AssignmentCompletionRule(string Kind, int Threshold, int RequiredCount);
