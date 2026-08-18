using Microsoft.EntityFrameworkCore;
using Sellevate.Learning.Features.TeamInsights.Models;
using Sellevate.Learning.Features.TeamInsights.Services.Abstract;
using Sellevate.Learning.Infrastructure.Data;
using Sellevate.Learning.Infrastructure.Identity;

namespace Sellevate.Learning.Features.TeamInsights.Services.Implementation;

/// <summary>
/// Phase 40.25. Builds the skill heat map and each manager's weakest funnel stage from the attempt
/// rows learning-service already owns (docs/TENANCY/ASSIGNMENTS.md §4).
///
/// <para>
/// <b>Computed here rather than in analytics-service, and that is the documented rule rather than a
/// convenience.</b> docs/ANALYTICS_SERVICE.md §"How learning metrics are counted by lesson version"
/// says it in as many words: analytics is Redis-only, stores no attempts and no lesson ids, and its
/// counters carry no organization label on purpose — a customer id as a Prometheus label puts
/// identities and unbounded cardinality into the monitoring store. Every number on this screen is
/// per-organization and per-person, so it comes from the database that holds the rows.
/// </para>
///
/// <para>
/// <b>Skill attribution goes through the mutable <c>Exercises</c> table, deliberately, and that is
/// not a contradiction of 40.16.</b> That phase bound an attempt to a <c>LessonVersion</c> so an
/// administrator fixing a wrong answer key could not silently re-score history — a rule about
/// <i>accuracy over time for one lesson</i>. "Which skill was this exercise about" is a different
/// question, it does not move when a typo is fixed, and reading it from the version snapshot would
/// pin the heat map to whichever taxonomy was live months ago. Attempts whose exercise no longer
/// exists therefore lose attribution rather than being guessed at, and are reported in their own
/// bucket the same way <c>unversionedAttempts</c> is.
/// </para>
/// </summary>
internal sealed class TeamSkillMapService(
    LearningDbContext databaseContext,
    IOrganizationMemberDirectory memberDirectory,
    ILogger<TeamSkillMapService> logger) : ITeamSkillMapService
{
    /// <summary>
    /// Below this many attempts a cell reports no percentage at all. Five is small enough that a
    /// week of practice qualifies and large enough that a single lucky answer cannot paint a cell
    /// green — the same argument 40.22 made for withholding an accuracy until a set is finished,
    /// applied to a matrix where every blank is an invitation to go and coach somebody.
    /// </summary>
    private const int MinimumAttemptsForAccuracy = 5;

    private const int DefaultWindowDays = 90;
    private const int MaximumWindowDays = 365;

    public async Task<TeamSkillMapDto> GetSkillMapAsync(
        int windowDays,
        CancellationToken cancellationToken = default)
    {
        var effectiveWindowDays = windowDays <= 0
            ? DefaultWindowDays
            : Math.Min(windowDays, MaximumWindowDays);

        var windowStart = DateTime.UtcNow.AddDays(-effectiveWindowDays);

        await using var tenantScope = await TenantTransactionScope.BeginReadAsync(databaseContext, cancellationToken);

        var attemptCells = await (
                from attempt in databaseContext.UserExerciseAttempts.AsNoTracking()
                where attempt.AttemptedAt >= windowStart
                join exercise in databaseContext.Exercises.AsNoTracking()
                    on attempt.ExerciseId equals exercise.Id
                join lesson in databaseContext.Lessons.AsNoTracking()
                    on exercise.LessonId equals lesson.Id
                join topic in databaseContext.Topics.AsNoTracking()
                    on lesson.TopicId equals topic.Id
                join skill in databaseContext.Skills.AsNoTracking()
                    on topic.SkillId equals skill.Id
                group attempt by new
                {
                    attempt.UserId,
                    skill.Id,
                    skill.Title,
                    skill.Stage,
                    skill.OrderInTree,
                }
                into grouped
                select new AttemptCell(
                    grouped.Key.UserId,
                    grouped.Key.Id,
                    grouped.Key.Title,
                    grouped.Key.Stage,
                    grouped.Key.OrderInTree,
                    grouped.Count(),
                    grouped.Count(attempt => attempt.IsCorrect)))
            .ToListAsync(cancellationToken);

        var totalAttempts = await databaseContext.UserExerciseAttempts
            .AsNoTracking()
            .CountAsync(attempt => attempt.AttemptedAt >= windowStart, cancellationToken);

        var dialogTotals = await databaseContext.UserDialogScores
            .AsNoTracking()
            .Where(score => score.EvaluatedAt >= windowStart)
            .GroupBy(score => score.UserId)
            .Select(grouped => new DialogTotal(
                grouped.Key,
                grouped.Count(),
                (int)Math.Round(grouped.Average(score => (double)score.Score))))
            .ToListAsync(cancellationToken);

        var stageLookup = await databaseContext.SkillStages
            .AsNoTracking()
            .OrderBy(stage => stage.Order)
            .ToDictionaryAsync(stage => stage.Key, cancellationToken);

        var roster = await TryReadRosterAsync(cancellationToken);

        var memberIds = roster is not null
            ? roster.ToList()
            : attemptCells
                .Select(cell => cell.UserId)
                .Concat(dialogTotals.Select(total => total.UserId))
                .Distinct()
                .ToList();

        var displayNames = await ReadDisplayNamesAsync(memberIds, cancellationToken);

        var skillColumns = attemptCells
            .GroupBy(cell => cell.SkillId)
            .Select(grouped =>
            {
                var first = grouped.First();
                var attempts = grouped.Sum(cell => cell.AttemptCount);

                return new TeamSkillMapSkillDto(
                    grouped.Key,
                    first.SkillTitle,
                    first.StageKey,
                    first.OrderInTree,
                    attempts,
                    Accuracy(attempts, grouped.Sum(cell => cell.CorrectCount)));
            })
            .OrderBy(skill => skill.StageKey)
            .ThenBy(skill => skill.OrderInTree)
            .ToList();

        var stages = attemptCells
            .GroupBy(cell => cell.StageKey)
            .Select(grouped =>
            {
                var attempts = grouped.Sum(cell => cell.AttemptCount);
                var descriptor = stageLookup.GetValueOrDefault(grouped.Key);

                return new TeamSkillMapStageDto(
                    grouped.Key,
                    // A stage key on a skill with no row in SkillStages is shown under its own key
                    // rather than dropped: dropping it would silently remove a column of the funnel
                    // from a screen whose whole claim is that it shows where the team sags.
                    descriptor?.Label ?? grouped.Key,
                    descriptor?.Accent ?? string.Empty,
                    descriptor?.Order ?? int.MaxValue,
                    attempts,
                    Accuracy(attempts, grouped.Sum(cell => cell.CorrectCount)));
            })
            .OrderBy(stage => stage.Order)
            .ThenBy(stage => stage.Key, StringComparer.Ordinal)
            .ToList();

        var cellsByUser = attemptCells.ToLookup(cell => cell.UserId);
        var dialogsByUser = dialogTotals.ToDictionary(total => total.UserId);

        var members = memberIds
            .Select(userId => BuildMember(
                userId,
                displayNames.GetValueOrDefault(userId),
                roster,
                cellsByUser[userId].ToList(),
                dialogsByUser.GetValueOrDefault(userId),
                stages))
            .OrderByDescending(member => member.AttemptCount + member.DialogCount)
            .ThenBy(member => member.DisplayName ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ThenBy(member => member.UserId)
            .ToList();

        return new TeamSkillMapDto(
            windowStart,
            stages,
            skillColumns,
            members,
            totalAttempts - attemptCells.Sum(cell => cell.AttemptCount),
            MinimumAttemptsForAccuracy,
            roster is not null);
    }

    private static TeamSkillMapMemberDto BuildMember(
        Guid userId,
        string? displayName,
        IReadOnlySet<Guid>? roster,
        IReadOnlyList<AttemptCell> cells,
        DialogTotal? dialogTotal,
        IReadOnlyList<TeamSkillMapStageDto> stages)
    {
        var attemptCount = cells.Sum(cell => cell.AttemptCount);
        var correctCount = cells.Sum(cell => cell.CorrectCount);

        var skillCells = cells
            .Select(cell => new TeamSkillMapCellDto(
                cell.SkillId.ToString(),
                cell.AttemptCount,
                Accuracy(cell.AttemptCount, cell.CorrectCount)))
            .ToList();

        var stageCells = cells
            .GroupBy(cell => cell.StageKey)
            .Select(grouped =>
            {
                var attempts = grouped.Sum(cell => cell.AttemptCount);

                return new TeamSkillMapCellDto(
                    grouped.Key,
                    attempts,
                    Accuracy(attempts, grouped.Sum(cell => cell.CorrectCount)));
            })
            // Same column order as the team-level stage list, so the screen can lay rows over
            // columns without re-sorting and without a missing stage shifting a row sideways.
            .OrderBy(cell => stages.FirstOrDefault(stage => stage.Key == cell.Key)?.Order ?? int.MaxValue)
            .ThenBy(cell => cell.Key, StringComparer.Ordinal)
            .ToList();

        var weakestStage = stageCells
            .Where(cell => cell.AccuracyPercent is not null)
            .OrderBy(cell => cell.AccuracyPercent)
            .ThenBy(cell => cell.Key, StringComparer.Ordinal)
            .FirstOrDefault();

        var weakestSkill = cells
            .Where(cell => Accuracy(cell.AttemptCount, cell.CorrectCount) is not null)
            .OrderBy(cell => Accuracy(cell.AttemptCount, cell.CorrectCount))
            .ThenBy(cell => cell.OrderInTree)
            .FirstOrDefault();

        return new TeamSkillMapMemberDto(
            userId,
            displayName,
            roster is null ? null : roster.Contains(userId),
            attemptCount,
            Accuracy(attemptCount, correctCount),
            weakestStage?.Key,
            weakestSkill?.SkillId,
            dialogTotal?.Count ?? 0,
            dialogTotal?.AverageScore,
            stageCells,
            skillCells);
    }

    private static int? Accuracy(int attemptCount, int correctCount)
        => attemptCount < MinimumAttemptsForAccuracy
            ? null
            : (int)Math.Round(100.0 * correctCount / attemptCount, MidpointRounding.AwayFromZero);

    /// <summary>
    /// Who works here, or <see langword="null"/> when identity-service could not be asked. Fail-open
    /// for the same reason the assignment dashboard's roster read is: the matrix is still true
    /// without it, and the cost of the outage is that people who have practised nothing are missing
    /// from the rows rather than that the screen is missing altogether.
    /// </summary>
    private async Task<IReadOnlySet<Guid>?> TryReadRosterAsync(CancellationToken cancellationToken)
    {
        try
        {
            return (await memberDirectory.GetRosterAsync(cancellationToken)).MemberIds.ToHashSet();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(
                exception,
                "The organization roster could not be read; the team skill map is being built from practice rows only.");

            return null;
        }
    }

    private async Task<Dictionary<Guid, string>> ReadDisplayNamesAsync(
        IReadOnlyList<Guid> userIds,
        CancellationToken cancellationToken)
    {
        if (userIds.Count == 0)
        {
            return [];
        }

        return await databaseContext.UserReplicas
            .AsNoTracking()
            .Where(replica => userIds.Contains(replica.UserId))
            .ToDictionaryAsync(replica => replica.UserId, replica => replica.DisplayName, cancellationToken);
    }

    private sealed record AttemptCell(
        Guid UserId,
        Guid SkillId,
        string SkillTitle,
        string StageKey,
        int OrderInTree,
        int AttemptCount,
        int CorrectCount);

    private sealed record DialogTotal(Guid UserId, int Count, int AverageScore);
}
