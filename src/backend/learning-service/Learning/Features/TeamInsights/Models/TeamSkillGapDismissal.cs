using Sellevate.BuildingBlocks.Tenancy;

namespace Sellevate.Learning.Features.TeamInsights.Models;

/// <summary>
/// Phase 40.31. One suggestion the РОП said no to (docs/TENANCY/ASSIGNMENTS.md §3.4).
///
/// <para>
/// <b>This is the only thing 40.31 stores, and that is the whole design.</b> The suggestions
/// themselves are computed on read, from the same heat map the screen draws — so a gap that closes
/// stops being offered without anything having to notice, exactly the way 40.18 computes staleness
/// and 40.25 computes the funnel. A stored suggestion would need a writer, an expiry sweep and a
/// rule for what happens to a row whose number has since moved, and all three would exist to hold a
/// fact the matrix already answers.
/// </para>
///
/// <para>
/// <b>A refusal is not derivable, so it is stored.</b> «Мы это знаем, у нас другой план на квартал»
/// is a decision a person made and nothing in the attempt rows implies it. Without it the dashboard
/// offers the same thing every week, the РОП learns to skip the panel, and the one week the offer
/// mattered it arrives in a box already trained to be ignored — the failure 40.26 avoided by not
/// sending a digest when nobody is late.
/// </para>
///
/// <para>
/// <b>Strict tenant data.</b> Plain equality in both the query filter and the row-level-security
/// policy: there is no global refusal, and a null owner would silence one organization's panel for
/// every other.
/// </para>
/// </summary>
public sealed class TeamSkillGapDismissal : ITenantScoped
{
    public Guid Id { get; set; }

    public Guid OrganizationId { get; set; }

    /// <summary>
    /// The <c>Skill.Stage</c> key that was dismissed — the identity half of
    /// <see cref="Sellevate.Learning.Common.Constants.SkillGapSourceRefs"/>, without the observation
    /// date. One live row per stage per organization, enforced by a unique index: a second refusal
    /// of the same stage replaces the first rather than stacking.
    /// </summary>
    public string StageKey { get; set; } = string.Empty;

    /// <summary>The РОП who dismissed it, or null when the token carried no user id.</summary>
    public Guid? DismissedBy { get; set; }

    public DateTime DismissedAt { get; set; }

    /// <summary>
    /// When the suggestion is allowed back.
    ///
    /// <para>
    /// <b>A refusal expires because a permanent one is a worse product than no button at all.</b>
    /// The team that was weak at closing in August is still being measured in November, and a
    /// dismissal that outlived its own evidence would quietly turn the panel off for the one stage
    /// most in need of it. The default life is the heat map's own default window — a refusal lasts
    /// exactly as long as the measurement that provoked it could still be the same measurement.
    /// </para>
    /// </summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// The team's accuracy on that stage at the moment of the refusal, 0-100.
    ///
    /// <para>
    /// Recorded so the refusal can be broken early by the world changing rather than only by the
    /// calendar: «мы это знаем» was said about 58%, and it is not an answer to 41%. See
    /// <c>TeamSkillGapService</c> for the drop that reopens it.
    /// </para>
    /// </summary>
    public int AccuracyPercentAtDismissal { get; set; }

    /// <summary>How much practice that number was built on, for the person reading the row later.</summary>
    public int AttemptCountAtDismissal { get; set; }

    /// <summary>Why, in the РОП's words. Optional, and never shown to the team.</summary>
    public string? Note { get; set; }
}
