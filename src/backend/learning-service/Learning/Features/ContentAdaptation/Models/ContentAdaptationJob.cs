using Sellevate.BuildingBlocks.Tenancy;
using Sellevate.Learning.Common.Constants;

namespace Sellevate.Learning.Features.ContentAdaptation.Models;

/// <summary>
/// Phase 40.32. One batch: «перепиши все упражнения этапа "закрытие" под наш продукт и тон», or the
/// same sweep asking what is wrong with them instead (roadmap 40.32).
///
/// <para>
/// <b>Strict tenant data, plain equality.</b> A batch names one customer's stage, holds proposals
/// written out of their product, their tone and their banned claims, and exists to be applied to
/// their library. There is no global batch and a null owner here would mean one customer's proposed
/// rewrites were readable — and acceptable — by every other. Same call as
/// <see cref="ContentGeneration.Models.ContentGenerationJob"/>, for the same reason: the content it
/// eventually touches is content-flavoured, the run that produced it never is.
/// </para>
///
/// <para>
/// <b>The batch is a lease and a scope; everything else about it is derived.</b> It owns no counter
/// of what is done — <see cref="Status"/> is recomputed from its items after every write, so a
/// crashed tick cannot leave a batch claiming to be finished while ten items still have no
/// proposal. What it does own is <see cref="ClaimedAt"/>, because a claim is the one fact that
/// cannot be derived from anything: it says a process is already spending money on this batch right
/// now.
/// </para>
///
/// <para>
/// <b>Nothing here can reach live content.</b> The batch writes rows in
/// <c>ContentAdaptationItems</c> and nowhere else; the only code that touches an
/// <c>Exercise</c> is the accept route, and it runs inside an administrator's request. That is the
/// roadmap's «никогда не автоприменение», and it is a property of which classes hold a
/// <c>DbSet&lt;Exercise&gt;</c> write rather than a rule anybody has to remember.
/// </para>
/// </summary>
public sealed class ContentAdaptationJob : ITenantScoped
{
    public Guid Id { get; set; }

    /// <summary>Owning organization; never null. See the class remarks for why the policy is equality.</summary>
    public Guid OrganizationId { get; set; }

    /// <summary>The РОП who started the batch.</summary>
    public Guid? CreatedBy { get; set; }

    /// <summary>One of <see cref="ContentAdaptationModes"/>.</summary>
    public string Mode { get; set; } = ContentAdaptationModes.ToneRewrite;

    /// <summary>
    /// The skill stage the batch covers — <c>Skill.Stage</c>, the same key the skill tree and 40.25's
    /// heat map use. «Этап "закрытие"» in the roadmap is this column holding <c>closing</c>.
    ///
    /// <para>
    /// A stage rather than a lesson because that is the unit a РОП thinks in and, more practically,
    /// because tone is a property of a whole stage's worth of wording: rewriting one lesson of six
    /// leaves the team reading two voices.
    /// </para>
    /// </summary>
    public string StageKey { get; set; } = string.Empty;

    /// <summary>One of <see cref="ContentAdaptationStatuses"/>. Derived from the items; see the class remarks.</summary>
    public string Status { get; set; } = ContentAdaptationStatuses.Preparing;

    /// <summary>
    /// How many exercises were in scope when the batch was created. Frozen at creation: it is the
    /// denominator of «сделано N из M» and of the cost estimate, and a denominator that moves while
    /// somebody is reviewing is worse than one that is slightly out of date.
    /// </summary>
    public int ItemCount { get; set; }

    /// <summary>
    /// When a worker took this batch, or null when it is free. Stamped and committed <b>before</b>
    /// any LLM call, so a process that dies mid-call leaves the batch visibly claimed and the lease —
    /// not a retry loop — is what releases it. Identical to 40.27's design and for the identical
    /// reason: a call that takes minutes cannot be held inside a transaction.
    /// </summary>
    public DateTime? ClaimedAt { get; set; }

    /// <summary>Why the last tick failed, for the person looking at a stuck batch. Per-item failures live on the item.</summary>
    public string? FailureReason { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    /// <summary>When the last item was answered by a person. Null while anything is still open.</summary>
    public DateTime? CompletedAt { get; set; }

    public ICollection<ContentAdaptationItem> Items { get; set; } = [];
}
