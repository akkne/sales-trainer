using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Sellevate.Learning.Common.Constants;
using Sellevate.Learning.Eventing;
using Sellevate.Learning.Features.Assignments.Models;
using Sellevate.Learning.Features.Assignments.Services.Abstract;
using Sellevate.Learning.Infrastructure.Data;
using Sellevate.Learning.Infrastructure.Identity;

namespace Sellevate.Learning.Features.Assignments.Services.Implementation;

/// <summary>
/// Phase 40.24. One organization's share of the repeat sweep: the block's actual subject, which is
/// that a training's effect decays in two to three weeks and a one-shot assignment reproduces
/// exactly that failure (docs/TENANCY/ASSIGNMENTS.md §2.1).
///
/// <para>
/// <b>A repeat is a new assignment row, not a second round inside the old one.</b> The alternative
/// has nowhere to put the second result: <c>AssignmentProgressRecords</c> carries one
/// <c>BestScore</c> and one <c>AttemptCount</c> per person, deliberately, and a second wave whose
/// score overwrote the first's would destroy the only evidence anybody had that the training decayed
/// — which is the fact this whole block exists to surface. A new row keeps each wave's funnel whole,
/// and <c>RepeatOfAssignmentId</c> is what lets 40.25 show the series as one thing anyway. The 40.21
/// freeze trigger says the same in its own words: "the answer to 'we want that practice again' is a
/// new assignment".
/// </para>
///
/// <para>
/// <b>Idempotency is derived from state, never counted.</b> A wave exists exactly when a row with
/// <c>(RepeatOfAssignmentId, RepeatWaveIndex)</c> exists, and a unique index enforces it. That is
/// 40.22's rule applied to a different table: nothing here increments anything, so a sweep that runs
/// twice inside one window issues one repeat, and a sweep that crashes halfway through an
/// organization re-issues nothing that already landed. It is also why the origin needs no "wave 1
/// sent" stamp — which matters more than it looks, because a **closed** assignment cannot be updated
/// at all (the 40.21 trigger freezes it whole), so a stamp on the origin would have made closing an
/// assignment silently cancel its repeats.
/// </para>
///
/// <para>
/// <b>The cohort is the origin's recipients, not a fresh resolution of its audience rule.</b> The
/// repeat is a refresher of a training that happened to particular people, and who those people were
/// is recorded exactly once — as the origin's progress rows. Re-resolving <c>whole_team</c> would
/// hand a shortened refresher of a session they never attended to everybody hired since, and it would
/// change the denominator between waves, which is precisely the comparison ("did it stick?") the
/// series exists to make. The live roster is still consulted, for the reason the deadline sweep
/// consults it: a progress row outlives employment on purpose, and an ex-employee must not be mailed
/// their former employer's homework.
/// </para>
/// </summary>
internal sealed class AssignmentRepeatIssueService(
    LearningDbContext databaseContext,
    IOrganizationMemberDirectory memberDirectory,
    ILearningEventPublisher eventPublisher,
    IOptions<AssignmentOptions> options,
    ILogger<AssignmentRepeatIssueService> logger) : IAssignmentRepeatIssueService
{
    public async Task<int> IssueDueRepeatsAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var dueWaves = await FindDueWavesAsync(now, cancellationToken);
        if (dueWaves.Count == 0)
        {
            return 0;
        }

        IReadOnlyList<Guid> activeMemberIds;
        try
        {
            activeMemberIds = (await memberDirectory.GetRosterAsync(cancellationToken)).MemberIds;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Skipped, not guessed. Nothing has been recorded as issued, so the next tick tries
            // again — the whole reason a background worker can afford the synchronous roster call
            // that 40.23 had to justify on an admin route.
            logger.LogWarning(
                exception,
                "Assignment repeats were skipped this tick: the organization roster could not be read.");

            return 0;
        }

        var roster = activeMemberIds.ToHashSet();
        var issuedCount = 0;

        foreach (var wave in dueWaves)
        {
            // One transaction per wave rather than one per organization: a wave that cannot be built
            // must not roll back a wave that can, and the unique index that catches a racing tick
            // should fail one insert rather than the tick. The transaction boundary alone did not
            // deliver that — without the catch below, a collision unwound the whole method and
            // abandoned every remaining due wave for the organization. Review, 40.34.
            try
            {
                if (await TryIssueWaveAsync(wave, roster, now, cancellationToken))
                {
                    issuedCount++;
                }
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogError(
                    exception,
                    "Failed to issue repeat wave {WaveIndex} of AssignmentId={AssignmentId}; continuing with the remaining waves",
                    wave.WaveIndex,
                    wave.Origin.Id);
            }
        }

        return issuedCount;
    }

    /// <summary>
    /// Every wave whose day has come and which has not been issued, in this organization.
    ///
    /// <para>
    /// <b>The late window is bounded.</b> A wave whose moment passed more than
    /// <c>RepeatCatchUpDays</c> ago is skipped rather than fired: the sweep coming back after a long
    /// outage — or meeting an assignment activated long before this block shipped — must not issue
    /// somebody a "+7 day" refresher for a training two months old, and must not issue both waves of
    /// a series in the same minute. Skipping is recomputed rather than recorded, so a skipped wave
    /// stays skipped without a row to say so.
    /// </para>
    /// </summary>
    private async Task<IReadOnlyList<DueRepeatWave>> FindDueWavesAsync(
        DateTime now,
        CancellationToken cancellationToken)
    {
        var catchUpWindow = TimeSpan.FromDays(Math.Clamp(options.Value.RepeatCatchUpDays, 1, 90));

        await using var tenantScope = await TenantTransactionScope.BeginReadAsync(databaseContext, cancellationToken);

        var origins = await databaseContext.Assignments
            .AsNoTracking()
            .Where(assignment => assignment.RepeatSchedule != null
                                 // Only an assignment a human created carries a schedule, and the
                                 // database enforces it. Reading it here as well keeps the series one
                                 // level deep even if the constraint is ever relaxed.
                                 && assignment.RepeatOfAssignmentId == null
                                 && assignment.ActivatedAt != null
                                 // A draft was never issued to anybody, so a repeat of it would be
                                 // the first time anybody heard of it. A closed one still repeats —
                                 // see the class summary.
                                 && assignment.Status != AssignmentStatuses.Draft)
            .ToListAsync(cancellationToken);

        if (origins.Count == 0)
        {
            return [];
        }

        // Typed nullable so the generated SQL compares the column itself rather than an unwrapped
        // projection of it — the same list, one less thing for the provider to translate.
        var originIds = origins.Select(origin => (Guid?)origin.Id).ToList();

        var issuedWaves = (await databaseContext.Assignments
                .AsNoTracking()
                .Where(assignment => assignment.RepeatOfAssignmentId != null
                                     && originIds.Contains(assignment.RepeatOfAssignmentId))
                .Select(assignment => new { assignment.RepeatOfAssignmentId, assignment.RepeatWaveIndex })
                .ToListAsync(cancellationToken))
            .Select(wave => (OriginId: wave.RepeatOfAssignmentId!.Value, WaveIndex: wave.RepeatWaveIndex ?? 0))
            .ToHashSet();

        var dueWaves = new List<DueRepeatWave>();

        foreach (var origin in origins)
        {
            var schedule = AssignmentRepeatScheduleReader.TryRead(origin.RepeatSchedule);
            if (schedule is null)
            {
                // Loud and harmless. 40.24 validates every schedule on the way in, so this can only
                // be a row written by another version of this service or by hand — and the safe
                // reading of an unreadable schedule is "does not repeat", never "repeats by default".
                logger.LogWarning(
                    "Assignment {AssignmentId} has a repeat schedule this service cannot read; it will not repeat.",
                    origin.Id);

                continue;
            }

            var anchor = origin.ActivatedAt!.Value;

            for (var offsetIndex = 0; offsetIndex < schedule.OffsetDays.Count; offsetIndex++)
            {
                var waveIndex = offsetIndex + 1;
                if (issuedWaves.Contains((origin.Id, waveIndex)))
                {
                    continue;
                }

                var dueAt = anchor.AddDays(schedule.OffsetDays[offsetIndex]);
                if (dueAt > now)
                {
                    continue;
                }

                if (dueAt < now - catchUpWindow)
                {
                    logger.LogInformation(
                        "Repeat wave {WaveIndex} of assignment {AssignmentId} was due {DueAt:O}, too long ago to issue now; skipping it permanently.",
                        waveIndex, origin.Id, dueAt);

                    continue;
                }

                dueWaves.Add(new DueRepeatWave(origin, waveIndex, schedule.OffsetDays[offsetIndex]));
            }
        }

        return dueWaves;
    }

    /// <summary>
    /// Builds and issues one wave. Returns whether it was created; every refusal is logged and left
    /// for the next tick or, when it cannot ever succeed, for the catch-up window to retire.
    /// </summary>
    private async Task<bool> TryIssueWaveAsync(
        DueRepeatWave wave,
        HashSet<Guid> roster,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var origin = wave.Origin;

        await using var tenantScope = await TenantTransactionScope.BeginWriteAsync(databaseContext, cancellationToken);

        var cohort = await databaseContext.AssignmentProgressRecords
            .AsNoTracking()
            .Where(record => record.AssignmentId == origin.Id)
            .Select(record => record.UserId)
            .ToListAsync(cancellationToken);

        // Everybody the origin was issued to, whatever became of them. Somebody who never started
        // and somebody who tried four times and stayed under the bar both belong here: excluding
        // them would mean the person the РОП most needs to reach is the one the product stops
        // asking, which inverts 40.22's whole argument for making failed_threshold visible.
        var recipientIds = cohort.Where(roster.Contains).Distinct().ToList();

        if (recipientIds.Count == 0)
        {
            logger.LogInformation(
                "Repeat wave {WaveIndex} of assignment {AssignmentId} has nobody left who still works here; not issued.",
                wave.WaveIndex, origin.Id);

            return false;
        }

        if (recipientIds.Count > AssignmentFanOut.MaximumFanOutSize)
        {
            logger.LogError(
                "Repeat wave {WaveIndex} of assignment {AssignmentId} would be issued to {RecipientCount} people, "
                + "over the {Maximum} limit; not issued.",
                wave.WaveIndex, origin.Id, recipientIds.Count, AssignmentFanOut.MaximumFanOutSize);

            return false;
        }

        var content = ShortenContent(origin.Content);
        if (content.Count == 0)
        {
            logger.LogWarning(
                "Assignment {AssignmentId} has no content to repeat; wave {WaveIndex} was not issued.",
                origin.Id, wave.WaveIndex);

            return false;
        }

        var repeat = new Assignment
        {
            Id = Guid.NewGuid(),
            // No human pressed anything, which is exactly what the nullable column 40.21 shipped is
            // for. Attributing the row to the РОП who wrote the origin would put their name on an act
            // they did not perform on a day they may no longer work here.
            CreatedBy = null,
            Title = BuildTitle(origin.Title, wave.WaveIndex),
            Goal = origin.Goal,
            SourceType = origin.SourceType,
            SourceRef = origin.SourceRef,
            Content = AssignmentDocumentSerializer.SerializeContent(content),
            // The resolved cohort, written as the rule it now is. Storing the origin's rule verbatim
            // would be a lie in the one column that is supposed to say who this is for: a repeat of
            // "the whole team" goes to the people the training happened to, not to the team as it
            // stands today.
            Audience = AssignmentDocumentSerializer.SerializeAudience(
                new AssignmentAudienceDto(AssignmentAudienceKinds.Users, recipientIds)),
            OpensAt = null,
            Deadline = BuildDeadline(origin, now),
            CompletionRule = ShortenCompletionRule(origin.CompletionRule),
            // Null, and the database says so too. A repeat that carried a schedule would repeat
            // itself, and two waves would each spawn two more.
            RepeatSchedule = null,
            RepeatOfAssignmentId = origin.Id,
            RepeatWaveIndex = wave.WaveIndex,
            // Issued on creation. "Configured once, then automatic" is the feature; a draft waiting
            // for somebody to press activate is a to-do item for the РОП, which is the thing the
            // roadmap says trainings die of.
            Status = AssignmentStatuses.Active,
            ActivatedAt = now,
            CreatedAt = now,
            UpdatedAt = now,
        };

        databaseContext.Assignments.Add(repeat);

        var issuedTo = await AssignmentFanOut.IssueAsync(
            databaseContext, eventPublisher, repeat, recipientIds, cancellationToken);

        await databaseContext.SaveChangesAsync(cancellationToken);
        await tenantScope.CommitAsync(cancellationToken);

        logger.LogInformation(
            "Assignment repeat issued RepeatId={RepeatId} OriginId={OriginId} Wave={WaveIndex} OffsetDays={OffsetDays} Recipients={RecipientCount}",
            repeat.Id, origin.Id, wave.WaveIndex, wave.OffsetDays, issuedTo);

        return true;
    }

    /// <summary>
    /// The "shortened version" the roadmap asks for, on the content side: theory is dropped and
    /// practice is kept.
    ///
    /// <para>
    /// That is the only reduction that is safe to make automatically. Dropping an exercise set or a
    /// conversation would change what the assignment measures, and the completion rule measures
    /// exactly those — but the reference material was read a fortnight ago by everybody who did the
    /// original, and re-reading it is the part of a refresher nobody does. Theory is kept when it is
    /// <b>all</b> the assignment has, because an assignment with no content asks people to do
    /// nothing.
    /// </para>
    /// </summary>
    private static IReadOnlyList<AssignmentContentItemDto> ShortenContent(string storedContent)
    {
        var items = AssignmentDocumentSerializer.DeserializeContent(storedContent);

        var practiceItems = items
            .Where(item => item.Kind != AssignmentContentItemKinds.ReferenceMaterial)
            .ToList();

        return practiceItems.Count > 0 ? practiceItems : items;
    }

    /// <summary>
    /// The same reduction on the rule side: fewer repetitions, never a lower bar.
    ///
    /// <para>
    /// <c>dialog_score</c> asks for half as many conversations, rounded up, minimum one — that is
    /// what makes a repeat cheap enough that people do it. <c>minimumScore</c> is untouched, and
    /// <c>exercise_accuracy</c> is returned verbatim: lowering a quality bar to make a repeat easier
    /// would turn the refresher into the four-minute completion 40.22 exists to make unreachable, and
    /// it would make the two waves' scores incomparable, which is the one thing the series is for.
    /// </para>
    ///
    /// <para>
    /// A rule this service cannot read is copied verbatim rather than rewritten — the same
    /// fail-closed reading <see cref="AssignmentCompletionRuleReader"/> asks its callers for.
    /// </para>
    /// </summary>
    private static string ShortenCompletionRule(string storedRule)
    {
        var rule = AssignmentCompletionRuleReader.TryRead(storedRule);
        if (rule is null || rule.Kind != AssignmentCompletionRuleKinds.DialogScore || rule.RequiredCount <= 1)
        {
            return storedRule;
        }

        var shortenedCount = (rule.RequiredCount + 1) / 2;

        return JsonSerializer.Serialize(new
        {
            kind = rule.Kind,
            minimumScore = rule.Threshold,
            requiredCount = shortenedCount,
        });
    }

    /// <summary>
    /// The repeat gets the same amount of time the original got, counted from now.
    ///
    /// <para>
    /// Copying the origin's absolute deadline would produce a repeat that is overdue the instant it
    /// is issued, and picking a fixed number of days would ignore a РОП who deliberately gives their
    /// team a fortnight. An origin with no deadline repeats with no deadline: open-ended is a choice
    /// the schema allows, and inventing urgency on that person's behalf is not this job's call. The
    /// one clamp is a floor of a day, for an origin activated after its own deadline had passed.
    /// </para>
    /// </summary>
    private static DateTime? BuildDeadline(Assignment origin, DateTime now)
    {
        if (origin.Deadline is not { } originDeadline || origin.ActivatedAt is not { } activatedAt)
        {
            return null;
        }

        var originalSpan = originDeadline - activatedAt;

        return now + (originalSpan < TimeSpan.FromDays(1) ? TimeSpan.FromDays(1) : originalSpan);
    }

    /// <summary>
    /// The wave is named in the title because the title is the only thing the recipient sees: it is
    /// what the notification body quotes and what the manager's strip shows. Two identically-named
    /// assignments a fortnight apart would read as a duplicate rather than as a refresher.
    /// </summary>
    private static string BuildTitle(string originTitle, int waveIndex)
    {
        var suffix = $" — повтор {waveIndex}";
        var room = AssignmentDocumentSerializer.MaximumTitleLength - suffix.Length;

        return (originTitle.Length <= room ? originTitle : originTitle[..room].TrimEnd()) + suffix;
    }

    /// <summary>One wave that is due now: which assignment it repeats, which wave it is, and its offset.</summary>
    private sealed record DueRepeatWave(Assignment Origin, int WaveIndex, int OffsetDays);
}
