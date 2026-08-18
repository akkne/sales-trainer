using Microsoft.EntityFrameworkCore;
using Sellevate.BuildingBlocks.Tenancy;
using Sellevate.Learning.Common.Constants;
using Sellevate.Learning.Features.Content.Services.Abstract;
using Sellevate.Learning.Features.ContentGeneration.Models;
using Sellevate.Learning.Features.ContentGeneration.Services.Abstract;
using Sellevate.Learning.Features.ContentGeneration.Services.Implementation;
using Sellevate.Learning.Features.TeamInsights.Models;
using Sellevate.Learning.Features.TeamInsights.Services.Abstract;
using Sellevate.Learning.Infrastructure.Data;

namespace Sellevate.Learning.Features.TeamInsights.Services.Implementation;

/// <summary>
/// Phase 40.31. Turns the heat map from a report into a tool: it names the stage of the sales funnel
/// the team is failing at, and starts the 40.27 content pipeline on it from one press
/// (docs/TENANCY/ASSIGNMENTS.md §3.4).
///
/// <para>
/// <b>The suggestion is derived from the same call that draws the map, not from a second query.</b>
/// <see cref="ITeamSkillMapService"/> already computes every number a gap is made of — the team's
/// accuracy per stage, each manager's cell, and the five-attempt floor below which a cell reports
/// nothing. Re-aggregating them here would let the panel and the matrix disagree about the same
/// window, and the one thing this feature must never do is offer to generate exercises for a cell
/// the РОП can see is green.
/// </para>
///
/// <para>
/// <b>Suggestions are computed; only refusals are stored.</b> The alternative — a table of proposed
/// gaps — needs a writer, an expiry rule and something to extinguish rows whose number has since
/// recovered, all to hold a fact the matrix already answers on every read. What genuinely cannot be
/// derived is that a person said no, so that is the one row this block adds
/// (<see cref="TeamSkillGapDismissal"/>). Same call 40.18 made for staleness and 40.25 for the funnel.
/// </para>
/// </summary>
internal sealed class TeamSkillGapService(
    LearningDbContext databaseContext,
    ITeamSkillMapService teamSkillMapService,
    IContentGenerationJobService contentGenerationJobService,
    IOrganizationProfileProvider organizationProfileProvider,
    ITenantContext tenantContext,
    ILogger<TeamSkillGapService> logger) : ITeamSkillGapService
{
    /// <summary>
    /// How much practice a stage needs before the team's number on it is allowed to trigger anything.
    ///
    /// <para>
    /// Four times the five attempts 40.25 requires of a single cell, and the multiplier is the whole
    /// argument: five attempts describe one person's afternoon, and this number is about a team. In
    /// practice twenty is five attempts from four managers, or a single active week — small enough
    /// that a real problem surfaces inside the weekly rhythm the block is designed around, large
    /// enough that a stage nobody has touched cannot be declared a failure.
    /// </para>
    /// </summary>
    private const int MinimumAttemptsForGap = 20;

    /// <summary>
    /// At or below this accuracy the stage counts as failing.
    ///
    /// <para>
    /// <b>A product decision taken by the agent, and this is the reasoning.</b> The bar the product
    /// already states for an individual is 40.22's own example of an <c>exercise_accuracy</c>
    /// completion rule: 80%. Triggering on "below the passing bar" would flag nearly every stage of
    /// every team and turn the panel into wallpaper — and a suggestion nobody reads is worse than
    /// none, because it also trains the РОП to skip the place the important one will appear (40.26's
    /// argument for not sending a digest when nobody is late). Twenty points below the passing bar is
    /// a different statement: not "needs practice" but "the team cannot do this". At that distance a
    /// suggestion is rare enough that pressing the button is a decision rather than a reflex.
    /// </para>
    /// </summary>
    private const int MaximumAccuracyPercentForGap = 60;

    /// <summary>
    /// How many managers must be below the bar before it is the team's failure rather than one
    /// person's.
    ///
    /// <para>
    /// One manager below the threshold is a coaching conversation, and 40.25 already names them:
    /// <c>TeamSkillMapMemberDto.WeakestStageKey</c> exists precisely so the РОП can go and talk to
    /// them. Generating content is expensive and changes what everybody on the team reads, so it has
    /// to answer to more than one person's bad week.
    /// </para>
    /// </summary>
    private const int MinimumStrugglingManagers = 2;

    /// <summary>
    /// How long a refusal holds.
    ///
    /// <para>
    /// The heat map's own default window (<c>TeamSkillMapService.DefaultWindowDays</c>). A refusal
    /// lasts exactly as long as the measurement that provoked it could still be the same
    /// measurement — after that every attempt behind the number has aged out of the window, the
    /// panel is looking at different evidence, and it is entitled to ask again.
    /// </para>
    /// </summary>
    private const int DismissalDays = 90;

    /// <summary>
    /// How far the number has to fall for a live refusal to be overruled.
    ///
    /// <para>
    /// «Мы это знаем» was said about one number. It is not an answer to a number ten points worse,
    /// and a refusal that survived a collapse would be the panel keeping quiet during the week it
    /// most needed to speak.
    /// </para>
    /// </summary>
    private const int ReopenAccuracyDropPercent = 10;

    /// <summary>
    /// How long a finished run keeps its stage off the panel. Content has to be reviewed, un-archived,
    /// assigned and practised before any of it can reach the heat map, and every week the panel
    /// re-offers work that is already sitting in the admin's queue is a week it teaches them to stop
    /// reading it.
    /// </summary>
    private const int RecentlyAddressedDays = 30;

    /// <summary>How many of the stage's weakest skills travel into the composed material.</summary>
    private const int MaximumWeakestSkills = 5;

    /// <summary>
    /// The states in which a run is still somebody's live work on that gap — including 40.28's
    /// <c>insufficient</c>, which is a question waiting on the РОП's desk and not a dead end.
    /// A second run started beside it would be a second identical question and a second bill.
    /// </summary>
    private static readonly string[] OpenRunStatuses =
    [
        ContentGenerationJobStatuses.Structuring,
        ContentGenerationJobStatuses.AwaitingReview,
        ContentGenerationJobStatuses.Generating,
        ContentGenerationJobStatuses.Insufficient
    ];

    public async Task<TeamSkillGapsDto> GetGapsAsync(
        int windowDays,
        CancellationToken cancellationToken = default)
        => (await ComputeAsync(windowDays, cancellationToken)).Gaps;

    public async Task<ContentGenerationJobDto> StartContentAsync(
        string stageKey,
        Guid? actorId,
        CancellationToken cancellationToken = default)
    {
        var normalizedStageKey = RequireUsableStageKey(stageKey);

        var (gaps, candidates) = await ComputeAsync(0, cancellationToken);

        // Pressing the button while a run for this stage is alive returns that run rather than
        // starting a second one. Double-submitting a form must not buy two lessons about the same
        // weakness — the same protection 40.27 built into approve, at the only other door into the
        // expensive half of the pipeline.
        var liveRunId = gaps.Suppressed
            .FirstOrDefault(suppressed =>
                suppressed.StageKey == normalizedStageKey
                && suppressed.Reason == TeamSkillGapSuppressionReasons.RunInProgress)
            ?.ContentGenerationJobId;

        if (liveRunId is not null)
        {
            var liveRun = await contentGenerationJobService.GetJobAsync(liveRunId.Value, cancellationToken);
            if (liveRun is not null)
            {
                return liveRun;
            }
        }

        // A dismissal and a recent run both suppress the offer; neither forbids the act. An
        // administrator who presses the button anyway has overruled both on purpose, and refusing
        // them would make the panel's opinion binding on its own reader.
        var gap = FindGap(candidates, gaps.WindowStart, normalizedStageKey)
                  ?? throw new TeamSkillGapStateException(
                      $"The team is not currently failing the '{normalizedStageKey}' funnel stage: a gap needs at least "
                      + $"{MinimumAttemptsForGap} attempts, accuracy at or below {MaximumAccuracyPercentForGap}% and at "
                      + $"least {MinimumStrugglingManagers} managers below it.");

        var profile = await organizationProfileProvider.GetCurrentAsync(cancellationToken);

        // Bounded to what the pipeline accepts rather than left to fail the run's own validation: a
        // customer with a thirty-term glossary would otherwise turn a button press into a 400 about
        // a string they never typed. A material trimmed at the cap still carries the measurement and
        // the product, which are the first things written.
        var material = TeamSkillGapMaterialComposer.Compose(
            gap, profile, ContentGenerationJobService.MaximumMaterialLength);

        await ClearDismissalAsync(normalizedStageKey, cancellationToken);

        var job = await contentGenerationJobService.StartAsync(
            new StartContentGenerationRequestDto(gap.ProposedTitle, material),
            actorId,
            gap.SourceRef,
            cancellationToken);

        logger.LogInformation(
            "Gap-detected content run started StageKey={StageKey} SourceRef={SourceRef} JobId={JobId} "
            + "AccuracyPercent={AccuracyPercent} AttemptCount={AttemptCount} ActorId={ActorId}",
            normalizedStageKey, gap.SourceRef, job.Id, gap.AccuracyPercent, gap.AttemptCount, actorId);

        return job;
    }

    public async Task<TeamSkillGapsDto> DismissAsync(
        string stageKey,
        DismissTeamSkillGapRequestDto requestDto,
        Guid? actorId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(requestDto);

        var normalizedStageKey = RequireUsableStageKey(stageKey);
        var organizationId = tenantContext.OrganizationId
            ?? throw new TeamSkillGapStateException("A dismissal belongs to exactly one organization.");

        var (gaps, candidates) = await ComputeAsync(0, cancellationToken);

        // Neither the stage's accuracy nor its attempt count comes from the request body. The row
        // records what the team actually scored at the moment of the refusal, because that number is
        // what later decides whether the refusal still applies — the same property 40.25 gave
        // DialogReviewNotes by reading the score from the row rather than from the caller.
        var gap = FindGap(candidates, gaps.WindowStart, normalizedStageKey)
                  ?? throw new TeamSkillGapStateException(
                      $"The team is not currently failing the '{normalizedStageKey}' funnel stage, so there is nothing to dismiss.");

        var note = (requestDto.Note ?? string.Empty).Trim();
        if (note.Length > 500)
        {
            throw new TeamSkillGapStateException("A dismissal note may hold at most 500 characters.");
        }

        var now = DateTime.UtcNow;

        await using var tenantScope = await TenantTransactionScope.BeginWriteAsync(databaseContext, cancellationToken);

        var dismissal = await databaseContext.TeamSkillGapDismissals
            .FirstOrDefaultAsync(candidate => candidate.StageKey == normalizedStageKey, cancellationToken);

        if (dismissal is null)
        {
            dismissal = new TeamSkillGapDismissal
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                StageKey = normalizedStageKey,
            };

            databaseContext.TeamSkillGapDismissals.Add(dismissal);
        }

        dismissal.DismissedBy = actorId;
        dismissal.DismissedAt = now;
        dismissal.ExpiresAt = now.AddDays(DismissalDays);
        dismissal.AccuracyPercentAtDismissal = gap.AccuracyPercent;
        dismissal.AttemptCountAtDismissal = gap.AttemptCount;
        dismissal.Note = note.Length == 0 ? null : note;

        await databaseContext.SaveChangesAsync(cancellationToken);
        await tenantScope.CommitAsync(cancellationToken);

        logger.LogInformation(
            "Skill gap dismissed StageKey={StageKey} AccuracyPercent={AccuracyPercent} ExpiresAt={ExpiresAt} ActorId={ActorId}",
            normalizedStageKey, gap.AccuracyPercent, dismissal.ExpiresAt, actorId);

        return (await ComputeAsync(0, cancellationToken)).Gaps;
    }

    public async Task<bool> RestoreAsync(string stageKey, CancellationToken cancellationToken = default)
    {
        var normalizedStageKey = RequireUsableStageKey(stageKey);

        await using var tenantScope = await TenantTransactionScope.BeginWriteAsync(databaseContext, cancellationToken);

        var dismissal = await databaseContext.TeamSkillGapDismissals
            .FirstOrDefaultAsync(candidate => candidate.StageKey == normalizedStageKey, cancellationToken);

        if (dismissal is null)
        {
            return false;
        }

        databaseContext.TeamSkillGapDismissals.Remove(dismissal);
        await databaseContext.SaveChangesAsync(cancellationToken);
        await tenantScope.CommitAsync(cancellationToken);

        return true;
    }

    private async Task<ComputedGaps> ComputeAsync(int windowDays, CancellationToken cancellationToken)
    {
        var skillMap = await teamSkillMapService.GetSkillMapAsync(windowDays, cancellationToken);
        var candidates = DetectCandidates(skillMap);

        if (candidates.Count == 0)
        {
            return new ComputedGaps(
                new TeamSkillGapsDto(
                    skillMap.WindowStart,
                    MinimumAttemptsForGap,
                    MaximumAccuracyPercentForGap,
                    MinimumStrugglingManagers,
                    [],
                    [],
                    skillMap.RosterKnown),
                candidates);
        }

        var now = DateTime.UtcNow;
        var candidateStageKeys = candidates.Select(candidate => candidate.StageKey).ToList();

        await using var tenantScope = await TenantTransactionScope.BeginReadAsync(databaseContext, cancellationToken);

        var dismissals = await databaseContext.TeamSkillGapDismissals
            .AsNoTracking()
            .Where(dismissal => candidateStageKeys.Contains(dismissal.StageKey) && dismissal.ExpiresAt > now)
            .ToListAsync(cancellationToken);

        var recentCutoff = now.AddDays(-RecentlyAddressedDays);

        // Every run this organization ever started from a gap that is either still alive or finished
        // recently. Filtered in memory by stage afterwards rather than with a LIKE per candidate: the
        // set is one administrator's runs from the last month, and a prefix predicate per stage would
        // be a query per red cell.
        var gapRuns = await databaseContext.ContentGenerationJobs
            .AsNoTracking()
            .Where(job => job.GapSourceRef != null
                          && (OpenRunStatuses.Contains(job.Status) || job.CreatedAt >= recentCutoff))
            .OrderByDescending(job => job.CreatedAt)
            .Select(job => new GapRun(job.Id, job.GapSourceRef!, job.Status, job.CreatedAt))
            .ToListAsync(cancellationToken);

        var runsByStage = gapRuns
            .Select(run => new { Run = run, StageKey = SkillGapSourceRefs.TryReadStageKey(run.GapSourceRef) })
            .Where(entry => entry.StageKey is not null)
            .ToLookup(entry => entry.StageKey!, entry => entry.Run);

        var gaps = new List<TeamSkillGapDto>();
        var suppressed = new List<SuppressedTeamSkillGapDto>();

        foreach (var candidate in candidates)
        {
            var runs = runsByStage[candidate.StageKey].ToList();

            var openRun = runs.FirstOrDefault(run => OpenRunStatuses.Contains(run.Status));
            if (openRun is not null)
            {
                suppressed.Add(Suppress(
                    candidate, TeamSkillGapSuppressionReasons.RunInProgress, null, openRun.JobId));
                continue;
            }

            var dismissal = dismissals.FirstOrDefault(row => row.StageKey == candidate.StageKey);
            if (dismissal is not null
                && candidate.AccuracyPercent > dismissal.AccuracyPercentAtDismissal - ReopenAccuracyDropPercent)
            {
                suppressed.Add(Suppress(
                    candidate, TeamSkillGapSuppressionReasons.Dismissed, dismissal.ExpiresAt, null));
                continue;
            }

            var finishedRun = runs.FirstOrDefault(run =>
                run.Status == ContentGenerationJobStatuses.Completed && run.CreatedAt >= recentCutoff);

            if (finishedRun is not null)
            {
                suppressed.Add(Suppress(
                    candidate,
                    TeamSkillGapSuppressionReasons.RecentlyAddressed,
                    finishedRun.CreatedAt.AddDays(RecentlyAddressedDays),
                    finishedRun.JobId));
                continue;
            }

            gaps.Add(ToGap(candidate, skillMap.WindowStart, now));
        }

        return new ComputedGaps(
            new TeamSkillGapsDto(
                skillMap.WindowStart,
                MinimumAttemptsForGap,
                MaximumAccuracyPercentForGap,
                MinimumStrugglingManagers,
                gaps,
                suppressed,
                skillMap.RosterKnown),
            candidates);
    }

    /// <summary>
    /// The three conditions, applied to the matrix 40.25 already computed. A stage qualifies when the
    /// team has practised it enough for the number to mean something, the number is bad, and it is
    /// bad for more than one person.
    /// </summary>
    private static List<GapCandidate> DetectCandidates(TeamSkillMapDto skillMap)
    {
        var candidates = new List<GapCandidate>();

        foreach (var stage in skillMap.Stages)
        {
            if (stage.AccuracyPercent is not { } accuracyPercent
                || accuracyPercent > MaximumAccuracyPercentForGap
                || stage.AttemptCount < MinimumAttemptsForGap
                || !SkillGapSourceRefs.IsUsableStageKey(stage.Key))
            {
                continue;
            }

            var measuredCells = skillMap.Members
                .Select(member => member.Stages.FirstOrDefault(cell => cell.Key == stage.Key))
                .Where(cell => cell?.AccuracyPercent is not null)
                .ToList();

            var strugglingCount = measuredCells
                .Count(cell => cell!.AccuracyPercent <= MaximumAccuracyPercentForGap);

            if (strugglingCount < MinimumStrugglingManagers)
            {
                continue;
            }

            var weakestSkills = skillMap.Skills
                .Where(skill => skill.StageKey == stage.Key && skill.AccuracyPercent is not null)
                .OrderBy(skill => skill.AccuracyPercent)
                .ThenBy(skill => skill.OrderInTree)
                .Take(MaximumWeakestSkills)
                .Select(skill => new TeamSkillGapSkillDto(
                    skill.SkillId, skill.Title, skill.AttemptCount, skill.AccuracyPercent!.Value))
                .ToList();

            candidates.Add(new GapCandidate(
                stage.Key,
                string.IsNullOrWhiteSpace(stage.Label) ? stage.Key : stage.Label,
                stage.AttemptCount,
                accuracyPercent,
                strugglingCount,
                measuredCells.Count,
                weakestSkills));
        }

        // Worst first. The panel shows the stage the team is furthest behind on at the top, because a
        // list of five equal-looking suggestions is a list nobody acts on.
        return candidates
            .OrderBy(candidate => candidate.AccuracyPercent)
            .ThenByDescending(candidate => candidate.StrugglingManagerCount)
            .ThenBy(candidate => candidate.StageKey, StringComparer.Ordinal)
            .ToList();
    }

    private static TeamSkillGapDto ToGap(GapCandidate candidate, DateTime windowStart, DateTime observedAt)
        => new(
            candidate.StageKey,
            candidate.StageLabel,
            SkillGapSourceRefs.Build(candidate.StageKey, observedAt),
            candidate.AttemptCount,
            candidate.AccuracyPercent,
            candidate.StrugglingManagerCount,
            candidate.MeasuredManagerCount,
            candidate.WeakestSkills,
            $"Слабый этап: {candidate.StageLabel}",
            $"Этап воронки продаж «{candidate.StageLabel}»: {candidate.AccuracyPercent}% верных ответов "
            + $"на {candidate.AttemptCount} попытках с {windowStart:dd.MM.yyyy} "
            + $"(порог провала — {MaximumAccuracyPercentForGap}%). "
            + $"Ниже порога {candidate.StrugglingManagerCount} из {candidate.MeasuredManagerCount} менеджеров.");

    private static SuppressedTeamSkillGapDto Suppress(
        GapCandidate candidate,
        string reason,
        DateTime? suppressedUntil,
        Guid? contentGenerationJobId)
        => new(
            candidate.StageKey,
            candidate.StageLabel,
            candidate.AttemptCount,
            candidate.AccuracyPercent,
            reason,
            suppressedUntil,
            contentGenerationJobId);

    /// <summary>
    /// A stage the measurement currently calls a failure, whether or not it is being offered. Acting
    /// on a suppressed gap is allowed — suppression governs what the panel proposes, not what the
    /// administrator may do — but a stage that is not failing at all cannot be acted on at any price.
    ///
    /// <para>
    /// It reads the candidate rather than the response, so the numbers a run or a dismissal records
    /// are the measured ones and never a reconstruction from what happened to be rendered.
    /// </para>
    /// </summary>
    private static TeamSkillGapDto? FindGap(
        IReadOnlyList<GapCandidate> candidates,
        DateTime windowStart,
        string stageKey)
    {
        var candidate = candidates.FirstOrDefault(entry => entry.StageKey == stageKey);

        return candidate is null ? null : ToGap(candidate, windowStart, DateTime.UtcNow);
    }

    private async Task ClearDismissalAsync(string stageKey, CancellationToken cancellationToken)
    {
        await using var tenantScope = await TenantTransactionScope.BeginWriteAsync(databaseContext, cancellationToken);

        var dismissal = await databaseContext.TeamSkillGapDismissals
            .FirstOrDefaultAsync(candidate => candidate.StageKey == stageKey, cancellationToken);

        if (dismissal is null)
        {
            return;
        }

        databaseContext.TeamSkillGapDismissals.Remove(dismissal);
        await databaseContext.SaveChangesAsync(cancellationToken);
        await tenantScope.CommitAsync(cancellationToken);
    }

    private static string RequireUsableStageKey(string? stageKey)
    {
        var normalizedStageKey = (stageKey ?? string.Empty).Trim();
        if (!SkillGapSourceRefs.IsUsableStageKey(normalizedStageKey))
        {
            throw new TeamSkillGapStateException(
                $"'{stageKey}' is not a usable funnel stage key.");
        }

        return normalizedStageKey;
    }

    private sealed record ComputedGaps(TeamSkillGapsDto Gaps, IReadOnlyList<GapCandidate> Candidates);

    private sealed record GapCandidate(
        string StageKey,
        string StageLabel,
        int AttemptCount,
        int AccuracyPercent,
        int StrugglingManagerCount,
        int MeasuredManagerCount,
        IReadOnlyList<TeamSkillGapSkillDto> WeakestSkills);

    private sealed record GapRun(Guid JobId, string GapSourceRef, string Status, DateTime CreatedAt);
}
