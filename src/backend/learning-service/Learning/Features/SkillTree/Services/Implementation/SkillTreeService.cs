using Microsoft.EntityFrameworkCore;
using Sellevate.Learning.Common.Constants;
using Sellevate.Learning.Features.SkillTree.Models;
using Sellevate.Learning.Features.SkillTree.Services.Abstract;
using Sellevate.Learning.Infrastructure.Data;

namespace Sellevate.Learning.Features.SkillTree.Services.Implementation;

internal sealed class SkillTreeService(LearningDbContext databaseContext) : ISkillTreeService
{
    /// <summary>
    /// Slug (<see cref="Skill.IconicName"/>) of the core skill every user is always
    /// enrolled in; it can never be unenrolled.
    /// </summary>
    private const string AlwaysEnrolledSlug = "sales-basics";

    public async Task<IReadOnlyList<SkillStageDto>> GetStagesAsync(
        CancellationToken cancellationToken = default)
    {
        return await databaseContext.SkillStages
            .OrderBy(stage => stage.Order)
            .Select(stage => new SkillStageDto(stage.Key, stage.Label, stage.Accent, stage.Order))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SkillTreeNodeDto>> GetAllSkillsAsync(
        CancellationToken cancellationToken = default)
    {
        var allSkills = await databaseContext.Skills
            .OrderBy(skill => skill.OrderInTree)
            .ThenBy(skill => skill.Id)
            .ToListAsync(cancellationToken);

        return allSkills.Select(skill => new SkillTreeNodeDto(
            skill.Id,
            skill.IconicName,
            skill.Title,
            "school",
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
        var allSkills = await databaseContext.Skills
            .OrderBy(skill => skill.OrderInTree)
            .ThenBy(skill => skill.Id)
            .ToListAsync(cancellationToken);

        // Enrollment: a UserSkillProgress row means the user is enrolled in that skill.
        // Back-compat: a user who has never enrolled in anything (no rows) is treated as
        // enrolled in everything, so existing accounts keep seeing the full tree until they
        // explicitly manage their skills.
        var enrolledSkillIds = await databaseContext.UserSkillProgressRecords
            .Where(record => record.UserId == userId)
            .Select(record => record.SkillId)
            .ToListAsync(cancellationToken);
        var enrolledSkillIdSet = enrolledSkillIds.ToHashSet();
        var hasAnyEnrollment = enrolledSkillIdSet.Count > 0;

        // Single query: lesson count per skill via topic join.
        var lessonCountBySkill = await databaseContext.Topics
            .Join(databaseContext.Lessons,
                topic => topic.Id,
                lesson => lesson.TopicId,
                (topic, lesson) => new { topic.SkillId, lesson.Id })
            .GroupBy(entry => entry.SkillId)
            .Select(group => new { SkillId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(entry => entry.SkillId, entry => entry.Count, cancellationToken);

        // Single query: completed lesson count per skill for this user.
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

        // Single query: most-recent lesson completion timestamp per skill for this user.
        // Uses UserLessonProgress.CompletedAt (same join chain as completedLessonCountBySkill above)
        // rather than UserExerciseAttempt because it requires one fewer join and mirrors the
        // existing query pattern exactly. Null CompletedAt rows are excluded so skills with no
        // completions naturally get null in the result dictionary.
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

            // Unenrolled skills surface as "locked" so the UI shows them as opt-in.
            var status = !isEnrolled ? LessonProgressStatuses.Locked :
                         completedLessons == 0 ? LessonProgressStatuses.Available :
                         completedLessons >= totalLessons && totalLessons > 0 ? LessonProgressStatuses.Completed :
                         LessonProgressStatuses.InProgress;

            return new SkillTreeNodeDto(
                skill.Id,
                skill.IconicName,
                skill.Title,
                "school",
                skill.OrderInTree,
                status,
                completedLessons,
                totalLessons,
                !isEnrolled,
                skill.Stage,
                lastActivityAt);
        }).ToList();
    }

    public async Task UpdateEnrolledSkillsAsync(
        Guid userId,
        IReadOnlyList<string> skillSlugs,
        CancellationToken cancellationToken = default)
    {
        // Always keep the core skill enrolled, regardless of what the caller sent.
        var desiredSlugs = skillSlugs
            .Append(AlwaysEnrolledSlug)
            .Distinct()
            .ToList();

        // Resolve slugs to real skill ids; silently ignore unknown slugs.
        var desiredSkillIds = await databaseContext.Skills
            .Where(skill => desiredSlugs.Contains(skill.IconicName))
            .Select(skill => skill.Id)
            .ToListAsync(cancellationToken);
        var desiredSkillIdSet = desiredSkillIds.ToHashSet();

        var existingRecords = await databaseContext.UserSkillProgressRecords
            .Where(record => record.UserId == userId)
            .ToListAsync(cancellationToken);
        var existingSkillIdSet = existingRecords.Select(record => record.SkillId).ToHashSet();

        // Remove enrollments the user no longer wants.
        var toRemove = existingRecords
            .Where(record => !desiredSkillIdSet.Contains(record.SkillId))
            .ToList();
        if (toRemove.Count > 0)
            databaseContext.UserSkillProgressRecords.RemoveRange(toRemove);

        // Add enrollments that don't exist yet.
        var toAdd = desiredSkillIdSet
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
        await databaseContext.UserSkillProgressRecords.AddRangeAsync(toAdd, cancellationToken);

        await databaseContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<TopicDto>> GetTopicsForSkillAsync(
        Guid skillId,
        CancellationToken cancellationToken = default)
    {
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

    public async Task<SkillTreeResponseDto> GetSkillTreeForUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
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
