using Microsoft.EntityFrameworkCore;
using Sellevate.Learning.Common.Constants;
using Sellevate.Learning.Features.Assignments.Models;
using Sellevate.Learning.Features.Assignments.Services.Abstract;
using Sellevate.Learning.Infrastructure.Data;

namespace Sellevate.Learning.Features.Assignments.Services.Implementation;

/// <summary>
/// Phase 40.23. The manager's side of an assignment (docs/TENANCY/ASSIGNMENTS.md §1).
///
/// <para>
/// <b>The query is over progress rows, not over assignments.</b> "Which assignments am I on" is
/// literally "which progress rows are mine", because 40.23 writes exactly one row per person at
/// issue time and that row is the record that they were asked. An assignment nobody issued to this
/// person cannot appear here however active it is, and no extra authorization check is needed to
/// make that true.
/// </para>
///
/// <para>
/// <b>Nothing here writes.</b> It would be natural to mark a row <c>in_progress</c> when its owner
/// first looks at it — and that would make this service the second writer of a column
/// <c>AssignmentThresholdEvaluator</c> owns (40.22), with a different idea of what "started" means:
/// opening a screen versus doing a piece of graded work. One writer per column is what keeps
/// <c>AttemptCount</c> and <c>FirstOpenedAt</c> worth reading, so the read stays a read.
/// </para>
/// </summary>
internal sealed class MyAssignmentService(LearningDbContext databaseContext) : IMyAssignmentService
{
    /// <summary>
    /// Phase 40.23. The manager's home-screen strip: what they currently owe, most urgent first.
    ///
    /// <para>
    /// <b>Soonest deadline first, and an open-ended assignment last rather than first.</b> Null sorts
    /// before every date in Postgres' default ordering, which would put the one assignment with no
    /// urgency at the top of a screen whose entire job is urgency. Sorted in memory because the set is
    /// one person's open assignments — single digits.
    /// </para>
    /// </summary>
    public async Task<IReadOnlyList<ActiveAssignmentDto>> GetActiveForUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
        {
            return [];
        }

        await using var tenantScope = await TenantTransactionScope.BeginReadAsync(databaseContext, cancellationToken);

        var rows = await ReadOpenRowsAsync(userId, cancellationToken);

        if (rows.Count == 0)
        {
            return [];
        }

        var contentByAssignment = rows.ToDictionary(
            row => row.Assignment.Id,
            row => AssignmentDocumentSerializer.DeserializeContent(row.Assignment.Content));

        var titles = await ResolveItemTitlesAsync(contentByAssignment.Values, cancellationToken);

        return rows
            .OrderBy(row => row.Assignment.Deadline ?? DateTime.MaxValue)
            .ThenByDescending(row => row.Assignment.ActivatedAt ?? row.Assignment.CreatedAt)
            .Select(row => new ActiveAssignmentDto(
                row.Assignment.Id,
                row.Assignment.Title,
                row.Assignment.Goal,
                row.Assignment.OpensAt,
                row.Assignment.Deadline,
                AssignmentDocumentSerializer.DeserializeRule(row.Assignment.CompletionRule),
                contentByAssignment[row.Assignment.Id]
                    .OrderBy(item => item.OrderIndex)
                    .Select(item => ToItemDto(item, titles))
                    .ToList(),
                row.Record.Status,
                row.Record.BestScore,
                row.Record.AttemptCount,
                row.Record.FirstOpenedAt,
                row.Record.CompletedAt))
            .ToList();
    }

    /// <summary>
    /// Phase 40.23. Finds the practice conversation this person owes on one dialog mode.
    ///
    /// <para>
    /// It shares <see cref="ReadOpenRowsAsync"/> with the screen above rather than writing a
    /// narrower query, which keeps one definition of "an assignment this person currently owes" —
    /// including the parts that are easy to forget, like an assignment whose opening date has not
    /// arrived. A second definition here would eventually let ai-service inject a persona for work
    /// the home screen does not show.
    /// </para>
    ///
    /// <para>
    /// It reads the <b>stored</b> content rather than the DTO the browser gets, because the persona
    /// is deliberately absent from that DTO: a persona the learner can read before the conversation
    /// starts is a rehearsal against a known script, and a persona the learner can send is one they
    /// can rewrite.
    /// </para>
    ///
    /// <para>
    /// Nearest deadline first, so when a repeat (40.24) and its original both name the same mode, the
    /// one they are closest to being late for wins.
    /// </para>
    /// </summary>
    public async Task<AssignmentPracticeContextDto?> GetPracticeContextAsync(
        Guid userId,
        string dialogModeKey,
        CancellationToken cancellationToken = default)
    {
        var modeKey = (dialogModeKey ?? string.Empty).Trim();
        if (userId == Guid.Empty || modeKey.Length == 0)
        {
            return null;
        }

        await using var tenantScope = await TenantTransactionScope.BeginReadAsync(databaseContext, cancellationToken);

        var rows = await ReadOpenRowsAsync(userId, cancellationToken);

        foreach (var row in rows.OrderBy(row => row.Assignment.Deadline ?? DateTime.MaxValue))
        {
            var item = AssignmentDocumentSerializer
                .DeserializeContent(row.Assignment.Content)
                .FirstOrDefault(candidate =>
                    candidate.Kind == AssignmentContentItemKinds.DialogScenario
                    && string.Equals(candidate.Reference, modeKey, StringComparison.Ordinal));

            if (item is null)
            {
                continue;
            }

            return new AssignmentPracticeContextDto(
                row.Assignment.Id,
                row.Assignment.Title,
                row.Assignment.Goal,
                item.Persona?.Name,
                item.Persona?.Position,
                item.Persona?.Personality,
                item.Persona?.Difficulty);
        }

        return null;
    }

    /// <summary>
    /// Phase 40.23. The one definition of "an assignment this person currently owes", shared by
    /// both public methods so the two can never drift apart.
    ///
    /// <para>
    /// Three parts of that definition are easy to get wrong. «Пока не выполнено» means a
    /// <c>completed</c> assignment stops taking the top of the screen, while <c>failed_threshold</c>
    /// stays — the work is finished and the bar was not met, and hiding it would leave the person who
    /// most needs another attempt with no way back to it. And an assignment scheduled to open later is
    /// not yet this person's problem.
    /// </para>
    /// </summary>
    private async Task<IReadOnlyList<OpenAssignmentRow>> ReadOpenRowsAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        return await (
                from record in databaseContext.AssignmentProgressRecords.AsNoTracking()
                join assignment in databaseContext.Assignments.AsNoTracking()
                    on record.AssignmentId equals assignment.Id
                where record.UserId == userId
                      && assignment.Status == AssignmentStatuses.Active
                      && record.Status != AssignmentProgressStatuses.Completed
                      && (assignment.OpensAt == null || assignment.OpensAt <= now)
                select new OpenAssignmentRow(record, assignment))
            .ToListAsync(cancellationToken);
    }

    private sealed record OpenAssignmentRow(AssignmentProgress Record, Assignment Assignment);

    private static ActiveAssignmentItemDto ToItemDto(
        AssignmentContentItemDto item,
        IReadOnlyDictionary<string, ResolvedItemTitle> titles)
    {
        var resolved = titles.GetValueOrDefault(BuildTitleKey(item.Kind, item.Reference));

        return new ActiveAssignmentItemDto(
            item.Kind,
            item.Reference,
            item.OrderIndex,
            resolved?.Title,
            resolved?.LessonId);
    }

    /// <summary>
    /// Resolves the display title of every referenced row in two queries rather than one per item.
    ///
    /// <para>
    /// A <c>dialog_scenario</c> reference is deliberately not resolved: it names an ai-service mode
    /// key, and learning-service asking ai-service for a title on every home-screen load would put
    /// a second service in the path of the first screen a manager sees. The key travels raw and the
    /// client — which already lists dialog modes to draw the practice screen — supplies the name.
    /// </para>
    /// </summary>
    private async Task<IReadOnlyDictionary<string, ResolvedItemTitle>> ResolveItemTitlesAsync(
        IEnumerable<IReadOnlyList<AssignmentContentItemDto>> contentSets,
        CancellationToken cancellationToken)
    {
        var allItems = contentSets.SelectMany(items => items).ToList();

        var lessonVersionIds = ParseReferenceIds(allItems, AssignmentContentItemKinds.LessonVersion);
        var referenceMaterialIds = ParseReferenceIds(allItems, AssignmentContentItemKinds.ReferenceMaterial);

        var titles = new Dictionary<string, ResolvedItemTitle>(StringComparer.Ordinal);

        if (lessonVersionIds.Count > 0)
        {
            var lessonVersions = await (
                    from version in databaseContext.LessonVersions.AsNoTracking()
                    join lesson in databaseContext.Lessons.AsNoTracking() on version.LessonId equals lesson.Id
                    where lessonVersionIds.Contains(version.Id)
                    select new { version.Id, version.LessonId, lesson.Title })
                .ToListAsync(cancellationToken);

            foreach (var lessonVersion in lessonVersions)
            {
                titles[BuildTitleKey(AssignmentContentItemKinds.LessonVersion, lessonVersion.Id.ToString())] =
                    new ResolvedItemTitle(lessonVersion.Title, lessonVersion.LessonId);
            }
        }

        if (referenceMaterialIds.Count > 0)
        {
            var materials = await databaseContext.ReferenceMaterials
                .AsNoTracking()
                .Where(material => referenceMaterialIds.Contains(material.Id))
                .Select(material => new { material.Id, material.Title })
                .ToListAsync(cancellationToken);

            foreach (var material in materials)
            {
                titles[BuildTitleKey(AssignmentContentItemKinds.ReferenceMaterial, material.Id.ToString())] =
                    new ResolvedItemTitle(material.Title, null);
            }
        }

        return titles;
    }

    private static List<Guid> ParseReferenceIds(IEnumerable<AssignmentContentItemDto> items, string kind)
        => items
            .Where(item => item.Kind == kind)
            .Select(item => Guid.TryParse(item.Reference, out var parsed) ? parsed : Guid.Empty)
            .Where(parsed => parsed != Guid.Empty)
            .Distinct()
            .ToList();

    /// <summary>
    /// Kind and reference together, because a reference is only unique inside its kind — the same
    /// uuid could in principle be a lesson version and a reference material.
    /// </summary>
    private static string BuildTitleKey(string kind, string reference) => $"{kind}|{reference}";

    private sealed record ResolvedItemTitle(string Title, Guid? LessonId);
}
