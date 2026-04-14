using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SalesTrainer.Api.Features.Lessons.Models;
using SalesTrainer.Api.Features.SkillTree.Models;
using SalesTrainer.Api.Infrastructure.Data;

namespace SalesTrainer.Api.Features.Admin;

public record SkillsImportResultDto(int SkillsCreated, int SkillsUpdated, List<string> Errors);
public record TopicsImportResultDto(int TopicsCreated, int TopicsUpdated, List<string> Errors);
public record LessonsImportResultDto(int LessonsCreated, int LessonsUpdated, int ExercisesCreated, int ExercisesUpdated, List<string> Errors);

/// <summary>
/// Import structure:
/// Skills JSON: [{ "iconicName": "...", "title": "...", "description": "...", "orderInTree": 1 }]
/// Topics JSON: [{ "skillIconicName": "...", "iconicName": "...", "title": "...", "orderInSkill": 1 }]
/// Lessons JSON: [{ "topicIconicName": "...", "title": "...", "orderInTopic": 1,
///                 "exercises": [{ "type": "...", "orderInLesson": 1, "content": {...}, "customAiPrompt": "..." }] }]
/// </summary>
[ApiController]
[Authorize(Policy = "RequireAdmin")]
public class AdminSeederController(AppDbContext db, ILogger<AdminSeederController> logger) : ControllerBase
{
    [HttpPost("admin/seeder/skills")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<ActionResult<SkillsImportResultDto>> ImportSkills(IFormFile file)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { message = "File is required." });

        if (!file.FileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { message = "Only .json files are accepted." });

        var existingSkills = await db.Skills.ToDictionaryAsync(s => s.IconicName);
        var state = new SkillsImportState();

        try
        {
            using var doc = await JsonDocument.ParseAsync(file.OpenReadStream());
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
                return BadRequest(new { message = "JSON must be an array of skill objects." });

            foreach (var (el, index) in doc.RootElement.EnumerateArray().Select((e, i) => (e, i + 1)))
            {
                try
                {
                    var iconicName = el.GetProperty("iconicName").GetString()?.Trim() ?? "";
                    var title = el.GetProperty("title").GetString()?.Trim() ?? "";
                    var description = el.TryGetProperty("description", out var descProp) && descProp.ValueKind != JsonValueKind.Null
                        ? descProp.GetString()?.Trim()
                        : null;
                    var orderInTree = el.GetProperty("orderInTree").GetInt32();
                    UpsertSkill(iconicName, title, description, orderInTree, existingSkills, state);
                }
                catch (Exception ex) { state.Errors.Add($"Item {index}: {ex.Message}"); }
            }
        }
        catch (JsonException ex) { return BadRequest(new { message = $"JSON parse error: {ex.Message}" }); }

        await db.SaveChangesAsync();

        var actorId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        logger.LogInformation(
            "Skills seeder import: SkillsCreated={SC} SkillsUpdated={SU} Errors={ErrCount} by ActorId={ActorId}",
            state.SkillsCreated, state.SkillsUpdated, state.Errors.Count, actorId);

        return Ok(new SkillsImportResultDto(state.SkillsCreated, state.SkillsUpdated, state.Errors));
    }

    private void UpsertSkill(string iconicName, string title, string? description, int orderInTree,
        Dictionary<string, Skill> existingSkills, SkillsImportState state)
    {
        if (string.IsNullOrWhiteSpace(iconicName)) throw new InvalidOperationException("iconicName is empty.");
        if (string.IsNullOrWhiteSpace(title)) throw new InvalidOperationException("title is empty.");

        if (existingSkills.TryGetValue(iconicName, out var found))
        {
            found.Title = title;
            found.Description = description;
            found.OrderInTree = orderInTree;
            if (state.UpdatedIconicNames.Add(iconicName)) state.SkillsUpdated++;
        }
        else
        {
            var skill = new Skill
            {
                Id = Guid.NewGuid(),
                IconicName = iconicName,
                Title = title,
                Description = description,
                OrderInTree = orderInTree
            };
            db.Skills.Add(skill);
            existingSkills[iconicName] = skill;
            if (state.CreatedIconicNames.Add(iconicName)) state.SkillsCreated++;
        }
    }

    [HttpPost("admin/seeder/topics")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<ActionResult<TopicsImportResultDto>> ImportTopics(IFormFile file)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { message = "File is required." });

        if (!file.FileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { message = "Only .json files are accepted." });

        var skillsByIconicName = await db.Skills.ToDictionaryAsync(s => s.IconicName);
        var existingTopics = await db.Topics.ToListAsync();
        var state = new TopicsImportState();

        try
        {
            using var doc = await JsonDocument.ParseAsync(file.OpenReadStream());
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
                return BadRequest(new { message = "JSON must be an array of topic objects." });

            foreach (var (el, index) in doc.RootElement.EnumerateArray().Select((e, i) => (e, i + 1)))
            {
                try
                {
                    var skillIconicName = el.GetProperty("skillIconicName").GetString()?.Trim() ?? "";
                    if (!skillsByIconicName.TryGetValue(skillIconicName, out var skill))
                    {
                        state.Errors.Add($"Item {index}: skill '{skillIconicName}' not found.");
                        continue;
                    }

                    var iconicName = el.GetProperty("iconicName").GetString()?.Trim() ?? "";
                    var title = el.GetProperty("title").GetString()?.Trim() ?? "";
                    var orderInSkill = el.GetProperty("orderInSkill").GetInt32();
                    UpsertTopic(skill.Id, iconicName, title, orderInSkill, existingTopics, state);
                }
                catch (Exception ex) { state.Errors.Add($"Item {index}: {ex.Message}"); }
            }
        }
        catch (JsonException ex) { return BadRequest(new { message = $"JSON parse error: {ex.Message}" }); }

        await db.SaveChangesAsync();

        var actorId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        logger.LogInformation(
            "Topics seeder import: TopicsCreated={TC} TopicsUpdated={TU} Errors={ErrCount} by ActorId={ActorId}",
            state.TopicsCreated, state.TopicsUpdated, state.Errors.Count, actorId);

        return Ok(new TopicsImportResultDto(state.TopicsCreated, state.TopicsUpdated, state.Errors));
    }

    private void UpsertTopic(Guid skillId, string iconicName, string title, int orderInSkill,
        List<Topic> existingTopics, TopicsImportState state)
    {
        if (string.IsNullOrWhiteSpace(iconicName)) throw new InvalidOperationException("iconicName is empty.");
        if (string.IsNullOrWhiteSpace(title)) throw new InvalidOperationException("title is empty.");

        var existing = existingTopics.FirstOrDefault(t => t.IconicName == iconicName);
        if (existing is not null)
        {
            existing.SkillId = skillId;
            existing.Title = title;
            existing.OrderInSkill = orderInSkill;
            state.TopicsUpdated++;
        }
        else
        {
            var topic = new Topic
            {
                Id = Guid.NewGuid(),
                SkillId = skillId,
                IconicName = iconicName,
                Title = title,
                OrderInSkill = orderInSkill
            };
            db.Topics.Add(topic);
            existingTopics.Add(topic);
            state.TopicsCreated++;
        }
    }

    [HttpPost("admin/seeder/lessons")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<ActionResult<LessonsImportResultDto>> ImportLessons(IFormFile file)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { message = "File is required." });

        if (!file.FileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { message = "Only .json files are accepted." });

        var topicsByIconicName = await db.Topics.ToDictionaryAsync(t => t.IconicName);
        var allLessons = await db.Lessons.ToListAsync();
        var allExercises = await db.Exercises.ToListAsync();
        var state = new LessonsImportState();

        try
        {
            using var doc = await JsonDocument.ParseAsync(file.OpenReadStream());
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
                return BadRequest(new { message = "JSON must be an array of lesson objects." });

            foreach (var (lessonEl, lessonIdx) in doc.RootElement.EnumerateArray().Select((e, i) => (e, i + 1)))
            {
                try
                {
                    var topicIconicName = lessonEl.GetProperty("topicIconicName").GetString()?.Trim() ?? "";
                    if (!topicsByIconicName.TryGetValue(topicIconicName, out var topic))
                    {
                        state.Errors.Add($"Item {lessonIdx}: topic '{topicIconicName}' not found.");
                        continue;
                    }

                    var lessonTitle = lessonEl.GetProperty("title").GetString()?.Trim() ?? "";
                    var orderInTopic = lessonEl.GetProperty("orderInTopic").GetInt32();

                    var lesson = UpsertLesson(topic.Id, lessonTitle, orderInTopic, allLessons, state);

                    if (lessonEl.TryGetProperty("exercises", out var exercisesEl)
                        && exercisesEl.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var (exEl, exIdx) in exercisesEl.EnumerateArray().Select((e, i) => (e, i + 1)))
                        {
                            try
                            {
                                var exType = exEl.GetProperty("type").GetString()?.Trim() ?? "";
                                var orderInLesson = exEl.GetProperty("orderInLesson").GetInt32();
                                var contentJson = exEl.GetProperty("content").GetRawText();
                                var customAiPrompt = exEl.TryGetProperty("customAiPrompt", out var promptProp) && promptProp.ValueKind != JsonValueKind.Null
                                    ? promptProp.GetString()
                                    : null;
                                UpsertExercise(lesson, exType, orderInLesson, contentJson, customAiPrompt, allExercises, state);
                            }
                            catch (Exception ex)
                            {
                                state.Errors.Add($"Item {lessonIdx}, exercise {exIdx}: {ex.Message}");
                            }
                        }
                    }
                }
                catch (Exception ex) { state.Errors.Add($"Item {lessonIdx}: {ex.Message}"); }
            }
        }
        catch (JsonException ex) { return BadRequest(new { message = $"JSON parse error: {ex.Message}" }); }

        await db.SaveChangesAsync();

        var actorId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        logger.LogInformation(
            "Lessons seeder import: LessonsCreated={LC} LessonsUpdated={LU} ExercisesCreated={EC} ExercisesUpdated={EU} Errors={ErrCount} by ActorId={ActorId}",
            state.LessonsCreated, state.LessonsUpdated, state.ExercisesCreated, state.ExercisesUpdated,
            state.Errors.Count, actorId);

        return Ok(new LessonsImportResultDto(
            state.LessonsCreated, state.LessonsUpdated,
            state.ExercisesCreated, state.ExercisesUpdated,
            state.Errors));
    }

    private Lesson UpsertLesson(Guid topicId, string title, int orderInTopic,
        List<Lesson> existingLessons, LessonsImportState state)
    {
        var existing = existingLessons.FirstOrDefault(l => l.TopicId == topicId && l.Title == title);
        if (existing is not null)
        {
            existing.OrderInTopic = orderInTopic;
            state.LessonsUpdated++;
            return existing;
        }

        var lesson = new Lesson
        {
            Id = Guid.NewGuid(),
            TopicId = topicId,
            Title = title,
            OrderInTopic = orderInTopic
        };
        db.Lessons.Add(lesson);
        existingLessons.Add(lesson);
        state.LessonsCreated++;
        return lesson;
    }

    private void UpsertExercise(Lesson lesson, string type, int orderInLesson, string contentJson, string? customAiPrompt,
        List<Exercise> existingExercises, LessonsImportState state)
    {
        var now = DateTime.UtcNow;
        var existing = existingExercises.FirstOrDefault(e => e.LessonId == lesson.Id && e.OrderInLesson == orderInLesson);
        if (existing is not null)
        {
            existing.Type = type;
            existing.SerializedContent = contentJson;
            existing.CustomAiPrompt = customAiPrompt;
            existing.UpdatedAt = now;
            state.ExercisesUpdated++;
        }
        else
        {
            var exercise = new Exercise
            {
                Id = Guid.NewGuid(),
                LessonId = lesson.Id,
                Type = type,
                OrderInLesson = orderInLesson,
                SerializedContent = contentJson,
                CustomAiPrompt = customAiPrompt,
                CreatedAt = now,
                UpdatedAt = now
            };
            db.Exercises.Add(exercise);
            existingExercises.Add(exercise);
            state.ExercisesCreated++;
        }
    }

    private sealed class SkillsImportState
    {
        public int SkillsCreated { get; set; }
        public int SkillsUpdated { get; set; }
        public List<string> Errors { get; } = [];
        public HashSet<string> CreatedIconicNames { get; } = [];
        public HashSet<string> UpdatedIconicNames { get; } = [];
    }

    private sealed class TopicsImportState
    {
        public int TopicsCreated { get; set; }
        public int TopicsUpdated { get; set; }
        public List<string> Errors { get; } = [];
    }

    private sealed class LessonsImportState
    {
        public int LessonsCreated { get; set; }
        public int LessonsUpdated { get; set; }
        public int ExercisesCreated { get; set; }
        public int ExercisesUpdated { get; set; }
        public List<string> Errors { get; } = [];
    }
}
