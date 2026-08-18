using Microsoft.EntityFrameworkCore;
using Sellevate.Learning.Common.Constants;
using Sellevate.Learning.Features.Content;
using Sellevate.Learning.Features.SkillTree.Models;
using Sellevate.Learning.Features.SkillTree.Services.Abstract;
using Sellevate.Learning.Infrastructure.Data;

namespace Sellevate.Learning.Features.SkillTree.Services.Implementation;

/// <summary>
/// Builds the learner-facing skill tree: the skill nodes, their per-user completion counts, and the
/// enrolled/locked state that decides which of them are offered.
///
/// <para>
/// <b>Enrollment is inferred from the presence of rows, not from a flag.</b> A
/// <see cref="UserSkillProgress"/> row means "enrolled in that skill", and a user with no rows at
/// all is treated as enrolled in everything. That back-compatibility branch exists so accounts
/// created before skill selection keep seeing the whole tree until they explicitly manage their
/// skills; removing it would silently lock every legacy account out of its own content. The core
/// skill named by <c>AlwaysEnrolledSlug</c> is enrolled unconditionally and cannot be dropped.
/// </para>
///
/// <para>
/// <b>Lesson counts are resolved for tenant overrides; completion counts are not, and the asymmetry
/// is deliberate (Phase 40.18).</b> The per-skill lesson total joins through
/// <c>Lessons.ResolveOverrides</c>, because an organization that overrode a lesson would otherwise
/// see it counted twice — once as the base row and once as its own copy — making every "3 of 7
/// lessons" wrong for exactly the customers who customized most. The completed-lesson and
/// last-activity queries start from the learner's own progress rows, which already point at whichever
/// single lesson row they actually answered, so resolving there would add a join and change nothing.
/// </para>
///
/// <para>
/// <b>The three per-user aggregates are each one grouped query, never a per-skill loop.</b> The tree
/// is rendered on the home screen, so the whole method must stay at a fixed number of round trips no
/// matter how many skills exist. Last activity is taken from <c>UserLessonProgress.CompletedAt</c>
/// rather than from attempt rows: it reuses the join chain the completion count already needs, and
/// rows with a null <c>CompletedAt</c> drop out so a skill with no completions simply has no entry.
/// </para>
/// </summary>
internal sealed class SkillTreeService(LearningDbContext databaseContext) : ISkillTreeService
{
    /// <summary>
    /// Slug (<see cref="Skill.IconicName"/>) of the core skill every user is always
    /// enrolled in; it can never be unenrolled.
    /// </summary>
    private const string AlwaysEnrolledSlug = "sales-basics";

    /// <summary>
    /// Material icon name every skill node is rendered with. Skills carry no per-row icon column,
    /// so this is the single place the tree's glyph is decided.
    /// </summary>
    private const string SkillNodeIconName = "school";

    public async Task<IReadOnlyList<SkillStageDto>> GetStagesAsync(
        CancellationToken cancellationToken = default)
    {
        await using var tenantScope = await TenantTransactionScope.BeginReadAsync(databaseContext, cancellationToken);

        return await databaseContext.SkillStages
            .OrderBy(stage => stage.Order)
            .Select(stage => new SkillStageDto(stage.Key, stage.Label, stage.Accent, stage.Order))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SkillTreeNodeDto>> GetAllSkillsAsync(
        CancellationToken cancellationToken = default)
    {
        await using var tenantScope = await TenantTransactionScope.BeginReadAsync(databaseContext, cancellationToken);

        var allSkills = await databaseContext.Skills
            .OrderBy(skill => skill.OrderInTree)
            .ThenBy(skill => skill.Id)
            .ToListAsync(cancellationToken);

        return allSkills.Select(skill => new SkillTreeNodeDto(
            skill.Id,
            skill.IconicName,
            skill.Title,
            SkillNodeIconName,
            skill.OrderInTree,
            LessonProgressStatuses.Available,
            0,
            0,
            false,
            skill.Stage,
            null))
            .ToList();
    }

    public async Task<IReadOnlyList<SkillTreeNodeDto>> GetAllSkillsWithProgressAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        await using var tenantScope = await TenantTransactionScope.BeginReadAsync(databaseContext, cancellationToken);

        var allSkills = await databaseContext.Skills
            .OrderBy(skill => skill.OrderInTree)
            .ThenBy(skill => skill.Id)
            .ToListAsync(cancellationToken);

        var enrolledSkillIds = await databaseContext.UserSkillProgressRecords
            .Where(record => record.UserId == userId)
            .Select(record => record.SkillId)
            .ToListAsync(cancellationToken);
        var enrolledSkillIdSet = enrolledSkillIds.ToHashSet();
        var hasAnyEnrollment = enrolledSkillIdSet.Count > 0;

        var lessonCountBySkill = await databaseContext.Topics
            .Join(databaseContext.Lessons.ResolveOverrides(databaseContext),
                topic => topic.Id,
                lesson => lesson.TopicId,
                (topic, lesson) => new { topic.SkillId, lesson.Id })
            .GroupBy(entry => entry.SkillId)
            .Select(group => new { SkillId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(entry => entry.SkillId, entry => entry.Count, cancellationToken);

        var completedLessonCountBySkill = await databaseContext.UserLessonProgressRecords
            .Where(progress => progress.UserId == userId && progress.Status == LessonProgressStatuses.Completed)
            .Join(databaseContext.Lessons,
                progress => progress.LessonId,
                lesson => lesson.Id,
                (progress, lesson) => lesson)
            .Join(databaseContext.Topics,
                lesson => lesson.TopicId,
                topic => topic.Id,
                (lesson, topic) => new { topic.SkillId })
            .GroupBy(entry => entry.SkillId)
            .Select(group => new { SkillId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(entry => entry.SkillId, entry => entry.Count, cancellationToken);

        var lastActivityBySkill = await databaseContext.UserLessonProgressRecords
            .Where(progress => progress.UserId == userId && progress.CompletedAt != null)
            .Join(databaseContext.Lessons,
                progress => progress.LessonId,
                lesson => lesson.Id,
                (progress, lesson) => new { lesson.TopicId, progress.CompletedAt })
            .Join(databaseContext.Topics,
                entry => entry.TopicId,
                topic => topic.Id,
                (entry, topic) => new { topic.SkillId, entry.CompletedAt })
            .GroupBy(entry => entry.SkillId)
            .Select(group => new { SkillId = group.Key, LastActivityAt = group.Max(e => e.CompletedAt) })
            .ToDictionaryAsync(entry => entry.SkillId, entry => entry.LastActivityAt, cancellationToken);

        return allSkills.Select(skill =>
        {
            var totalLessons = lessonCountBySkill.GetValueOrDefault(skill.Id, 0);
            var completedLessons = completedLessonCountBySkill.GetValueOrDefault(skill.Id, 0);
            lastActivityBySkill.TryGetValue(skill.Id, out var lastActivityAt);

            var isEnrolled = !hasAnyEnrollment
                             || skill.IconicName == AlwaysEnrolledSlug
                             || enrolledSkillIdSet.Contains(skill.Id);

            var status = !isEnrolled ? LessonProgressStatuses.Locked :
                         completedLessons == 0 ? LessonProgressStatuses.Available :
                         completedLessons >= totalLessons && totalLessons > 0 ? LessonProgressStatuses.Completed :
                         LessonProgressStatuses.InProgress;

            return new SkillTreeNodeDto(
                skill.Id,
                skill.IconicName,
                skill.Title,
                SkillNodeIconName,
                skill.OrderInTree,
                status,
                completedLessons,
                totalLessons,
                !isEnrolled,
                skill.Stage,
                lastActivityAt);
        }).ToList();
    }

    /// <summary>
    /// Reconciles the user's enrollment rows with <paramref name="skillSlugs"/>: rows for skills no
    /// longer wanted are deleted, missing ones inserted, and existing ones left untouched so their
    /// accumulated progress survives a re-selection.
    ///
    /// <para>
    /// <b>Two deliberate leniencies.</b> The core skill is appended to the requested set whatever the
    /// caller sent, so it can never be unenrolled; and slugs that match no skill are dropped silently
    /// rather than rejected, so a stale client that still knows a retired skill can still save the
    /// rest of its selection.
    /// </para>
    ///
    /// <para>
    /// <b>Deleting a row discards that skill's progress counters.</b> Unenrolling is not a soft hide:
    /// re-enrolling later starts the skill from <c>Available</c> with zero counts. The learner's
    /// per-lesson history in <c>UserLessonProgress</c> is untouched, so the tree's completion counts
    /// come back as soon as they re-enroll.
    /// </para>
    /// </summary>
    public async Task UpdateEnrolledSkillsAsync(
        Guid userId,
        IReadOnlyList<string> skillSlugs,
        CancellationToken cancellationToken = default)
    {
        await using var tenantScope = await TenantTransactionScope.BeginWriteAsync(databaseContext, cancellationToken);

        var desiredSlugs = skillSlugs
            .Append(AlwaysEnrolledSlug)
            .Distinct()
            .ToList();

        var desiredSkillIds = await databaseContext.Skills
            .Where(skill => desiredSlugs.Contains(skill.IconicName))
            .Select(skill => skill.Id)
            .ToListAsync(cancellationToken);
        var desiredSkillIdSet = desiredSkillIds.ToHashSet();

        var existingRecords = await databaseContext.UserSkillProgressRecords
            .Where(record => record.UserId == userId)
            .ToListAsync(cancellationToken);
        var existingSkillIdSet = existingRecords.Select(record => record.SkillId).ToHashSet();

        var recordsToRemove = existingRecords
            .Where(record => !desiredSkillIdSet.Contains(record.SkillId))
            .ToList();
        if (recordsToRemove.Count > 0)
            databaseContext.UserSkillProgressRecords.RemoveRange(recordsToRemove);

        var recordsToAdd = desiredSkillIdSet
            .Where(skillId => !existingSkillIdSet.Contains(skillId))
            .Select(skillId => new UserSkillProgress
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                SkillId = skillId,
                Status = LessonProgressStatuses.Available,
                CompletedLessonCount = 0,
                TotalLessonCount = 0,
            });
        await databaseContext.UserSkillProgressRecords.AddRangeAsync(recordsToAdd, cancellationToken);

        await databaseContext.SaveChangesAsync(cancellationToken);
        await tenantScope.CommitAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<TopicDto>> GetTopicsForSkillAsync(
        Guid skillId,
        CancellationToken cancellationToken = default)
    {
        await using var tenantScope = await TenantTransactionScope.BeginReadAsync(databaseContext, cancellationToken);

        var topics = await databaseContext.Topics
            .Where(topic => topic.SkillId == skillId)
            .OrderBy(topic => topic.OrderInSkill)
            .ToListAsync(cancellationToken);

        return topics.Select(topic => new TopicDto(
            topic.Id,
            topic.SkillId,
            topic.Title,
            topic.OrderInSkill))
            .ToList();
    }

    /// <summary>
    /// Wraps the per-user skill nodes in the tree response the client screen consumes.
    ///
    /// <para>
    /// <b>The streak and experience-point fields are always zero, and that is not a stub.</b>
    /// Gamification was removed from the product; the fields survive only so the response contract
    /// stays stable for clients that still deserialize them, and nothing renders them. Do not start
    /// populating them without the product decision being reversed first.
    /// </para>
    /// </summary>
    public async Task<SkillTreeResponseDto> GetSkillTreeForUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        await using var tenantScope = await TenantTransactionScope.BeginReadAsync(databaseContext, cancellationToken);

        var allSkills = await GetAllSkillsWithProgressAsync(userId, cancellationToken);

        return new SkillTreeResponseDto(
            allSkills,
            CurrentStreakDayCount: 0,
            TotalXpAmount: 0,
            WeeklyXpAmount: 0,
            DailyXpAmount: 0,
            DailyXpGoal: 0,
            WeeklyXpGoal: 0);
    }
}
