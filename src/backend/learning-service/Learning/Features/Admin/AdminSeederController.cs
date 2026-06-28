using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sellevate.Learning.Common.Constants;
using Sellevate.Learning.Features.Exercises.Services;
using Sellevate.Learning.Features.Lessons.Models;
using Sellevate.Learning.Features.SkillTree.Models;
using Sellevate.Learning.Infrastructure.Data;

namespace Sellevate.Learning.Features.Admin;

[ApiController]
[Authorize(Policy = AuthorizationPolicies.RequireAdministrator)]
public sealed class AdminSeederController(LearningDbContext database, ILogger<AdminSeederController> logger) : ControllerBase
{
    [HttpPost("admin/seeder/skills")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<ActionResult<SkillsImportResultDto>> ImportSkills(IFormFile file, CancellationToken cancellationToken = default)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { message = "File is required." });

        if (!file.FileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { message = "Only .json files are accepted." });

        var existingSkills = await database.Skills.ToDictionaryAsync(skill => skill.IconicName, cancellationToken);
        var state = new SkillsImportState();

        try
        {
            using var doc = await JsonDocument.ParseAsync(file.OpenReadStream());
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
                return BadRequest(new { message = "JSON must be an array of skill objects." });

            foreach (var (element, index) in doc.RootElement.EnumerateArray().Select((element, index) => (element, index + 1)))
            {
                try
                {
                    var iconicName = element.GetProperty("iconicName").GetString()?.Trim() ?? "";
                    var title = element.GetProperty("title").GetString()?.Trim() ?? "";
                    var description = element.TryGetProperty("description", out var descProp) && descProp.ValueKind != JsonValueKind.Null
                        ? descProp.GetString()?.Trim()
                        : null;
                    var orderInTree = element.GetProperty("orderInTree").GetInt32();
                    var stage = element.TryGetProperty("stage", out var stageProp) && stageProp.ValueKind == JsonValueKind.String
                        ? stageProp.GetString()?.Trim()
                        : null;
                    UpsertSkill(iconicName, title, description, orderInTree, stage, existingSkills, state);
                }
                catch (Exception exception) { state.Errors.Add($"Item {index}: {exception.Message}"); }
            }
        }
        catch (JsonException exception) { return BadRequest(new { message = $"JSON parse error: {exception.Message}" }); }

        await database.SaveChangesAsync(cancellationToken);

        var actorId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        logger.LogInformation(
            "Skills seeder import: SkillsCreated={SkillsCreated} SkillsUpdated={SkillsUpdated} Errors={ErrorCount} by ActorId={ActorId}",
            state.SkillsCreated, state.SkillsUpdated, state.Errors.Count, actorId);

        return Ok(new SkillsImportResultDto(state.SkillsCreated, state.SkillsUpdated, state.Errors));
    }

    private void UpsertSkill(string iconicName, string title, string? description, int orderInTree, string? stage,
        Dictionary<string, Skill> existingSkills, SkillsImportState state)
    {
        if (string.IsNullOrWhiteSpace(iconicName)) throw new InvalidOperationException("iconicName is empty.");
        if (string.IsNullOrWhiteSpace(title)) throw new InvalidOperationException("title is empty.");

        if (existingSkills.TryGetValue(iconicName, out var found))
        {
            found.Title = title;
            found.Description = description;
            found.OrderInTree = orderInTree;
            if (!string.IsNullOrWhiteSpace(stage)) found.Stage = stage;
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
                OrderInTree = orderInTree,
                Stage = string.IsNullOrWhiteSpace(stage) ? "general" : stage
            };
            database.Skills.Add(skill);
            existingSkills[iconicName] = skill;
            if (state.CreatedIconicNames.Add(iconicName)) state.SkillsCreated++;
        }
    }

    [HttpPost("admin/seeder/topics")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<ActionResult<TopicsImportResultDto>> ImportTopics(IFormFile file, CancellationToken cancellationToken = default)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { message = "File is required." });

        if (!file.FileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { message = "Only .json files are accepted." });

        var skillsByIconicName = await database.Skills.ToDictionaryAsync(skill => skill.IconicName, cancellationToken);
        var existingTopics = await database.Topics.ToListAsync(cancellationToken);
        var state = new TopicsImportState();

        try
        {
            using var doc = await JsonDocument.ParseAsync(file.OpenReadStream());
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
                return BadRequest(new { message = "JSON must be an array of topic objects." });

            foreach (var (element, index) in doc.RootElement.EnumerateArray().Select((element, index) => (element, index + 1)))
            {
                try
                {
                    var skillIconicName = element.GetProperty("skillIconicName").GetString()?.Trim() ?? "";
                    if (!skillsByIconicName.TryGetValue(skillIconicName, out var skill))
                    {
                        state.Errors.Add($"Item {index}: skill '{skillIconicName}' not found.");
                        continue;
                    }

                    var iconicName = element.GetProperty("iconicName").GetString()?.Trim() ?? "";
                    var title = element.GetProperty("title").GetString()?.Trim() ?? "";
                    var orderInSkill = element.GetProperty("orderInSkill").GetInt32();
                    UpsertTopic(skill.Id, iconicName, title, orderInSkill, existingTopics, state);
                }
                catch (Exception exception) { state.Errors.Add($"Item {index}: {exception.Message}"); }
            }
        }
        catch (JsonException exception) { return BadRequest(new { message = $"JSON parse error: {exception.Message}" }); }

        await database.SaveChangesAsync(cancellationToken);

        var actorId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        logger.LogInformation(
            "Topics seeder import: TopicsCreated={TopicsCreated} TopicsUpdated={TopicsUpdated} Errors={ErrorCount} by ActorId={ActorId}",
            state.TopicsCreated, state.TopicsUpdated, state.Errors.Count, actorId);

        return Ok(new TopicsImportResultDto(state.TopicsCreated, state.TopicsUpdated, state.Errors));
    }

    private void UpsertTopic(Guid skillId, string iconicName, string title, int orderInSkill,
        List<Topic> existingTopics, TopicsImportState state)
    {
        if (string.IsNullOrWhiteSpace(iconicName)) throw new InvalidOperationException("iconicName is empty.");
        if (string.IsNullOrWhiteSpace(title)) throw new InvalidOperationException("title is empty.");

        var existing = existingTopics.FirstOrDefault(topic => topic.IconicName == iconicName);
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
            database.Topics.Add(topic);
            existingTopics.Add(topic);
            state.TopicsCreated++;
        }
    }

    [HttpPost("admin/seeder/lessons")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<ActionResult<LessonsImportResultDto>> ImportLessons(IFormFile file, CancellationToken cancellationToken = default)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { message = "File is required." });

        if (!file.FileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { message = "Only .json files are accepted." });

        var topicsByIconicName = await database.Topics.ToDictionaryAsync(topic => topic.IconicName, cancellationToken);
        var allLessons = await database.Lessons.ToListAsync(cancellationToken);
        var allExercises = await database.Exercises.ToListAsync(cancellationToken);
        var state = new LessonsImportState();

        try
        {
            using var doc = await JsonDocument.ParseAsync(file.OpenReadStream());
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
                return BadRequest(new { message = "JSON must be an array of lesson objects." });

            foreach (var (lessonElement, lessonIndex) in doc.RootElement.EnumerateArray().Select((element, index) => (element, index + 1)))
            {
                try
                {
                    var topicIconicName = lessonElement.GetProperty("topicIconicName").GetString()?.Trim() ?? "";
                    if (!topicsByIconicName.TryGetValue(topicIconicName, out var topic))
                    {
                        state.Errors.Add($"Item {lessonIndex}: topic '{topicIconicName}' not found.");
                        continue;
                    }

                    var lessonTitle = lessonElement.GetProperty("title").GetString()?.Trim() ?? "";
                    var orderInTopic = lessonElement.GetProperty("orderInTopic").GetInt32();

                    var lesson = UpsertLesson(topic.Id, lessonTitle, orderInTopic, allLessons, state);

                    if (lessonElement.TryGetProperty("exercises", out var exercisesElement)
                        && exercisesElement.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var (exerciseElement, exerciseIndex) in exercisesElement.EnumerateArray().Select((element, index) => (element, index + 1)))
                        {
                            try
                            {
                                var exerciseType = exerciseElement.GetProperty("type").GetString()?.Trim() ?? "";
                                var orderInLesson = exerciseElement.GetProperty("orderInLesson").GetInt32();
                                var contentElement = exerciseElement.GetProperty("content");
                                var customAiPrompt = exerciseElement.TryGetProperty("customAiPrompt", out var promptProp) && promptProp.ValueKind != JsonValueKind.Null
                                    ? promptProp.GetString()
                                    : null;

                                var contentErrors = ExerciseContentValidator.Validate(exerciseType, contentElement);
                                if (contentErrors.Count > 0)
                                {
                                    state.Errors.Add($"Item {lessonIndex}, exercise {exerciseIndex} ({exerciseType}): {string.Join(" ", contentErrors)}");
                                    continue;
                                }

                                UpsertExercise(lesson, exerciseType, orderInLesson, contentElement.GetRawText(), customAiPrompt, allExercises, state);
                            }
                            catch (Exception exception)
                            {
                                state.Errors.Add($"Item {lessonIndex}, exercise {exerciseIndex}: {exception.Message}");
                            }
                        }
                    }
                }
                catch (Exception exception) { state.Errors.Add($"Item {lessonIndex}: {exception.Message}"); }
            }
        }
        catch (JsonException exception) { return BadRequest(new { message = $"JSON parse error: {exception.Message}" }); }

        await database.SaveChangesAsync(cancellationToken);

        var actorId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        logger.LogInformation(
            "Lessons seeder import: LessonsCreated={LessonsCreated} LessonsUpdated={LessonsUpdated} ExercisesCreated={ExercisesCreated} ExercisesUpdated={ExercisesUpdated} Errors={ErrorCount} by ActorId={ActorId}",
            state.LessonsCreated, state.LessonsUpdated, state.ExercisesCreated, state.ExercisesUpdated,
            state.Errors.Count, actorId);

        return Ok(new LessonsImportResultDto(
            state.LessonsCreated, state.LessonsUpdated,
            state.ExercisesCreated, state.ExercisesUpdated,
            state.Errors));
    }

    [HttpPost("admin/seeder/bundle")]
    [RequestSizeLimit(20 * 1024 * 1024)]
    public async Task<ActionResult<BundleImportResultDto>> ImportBundle(IFormFile file, CancellationToken cancellationToken = default)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { message = "File is required." });

        if (!file.FileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { message = "Only .json files are accepted." });

        var existingSkills = await database.Skills.ToDictionaryAsync(skill => skill.IconicName, cancellationToken);
        var existingTopics = await database.Topics.ToListAsync(cancellationToken);
        var allLessons = await database.Lessons.ToListAsync(cancellationToken);
        var allExercises = await database.Exercises.ToListAsync(cancellationToken);

        var skillsState = new SkillsImportState();
        var topicsState = new TopicsImportState();
        var lessonsState = new LessonsImportState();
        var errors = new List<string>();

        try
        {
            using var doc = await JsonDocument.ParseAsync(file.OpenReadStream());
            var root = doc.RootElement;

            if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("skills", out var skillsProp))
                root = skillsProp;
            if (root.ValueKind != JsonValueKind.Array)
                return BadRequest(new { message = "JSON must be an object { \"skills\": [...] } or an array of skill objects." });

            foreach (var (skillElement, skillIndex) in root.EnumerateArray().Select((element, index) => (element, index + 1)))
            {
                var skillIconicName = "";
                try
                {
                    skillIconicName = skillElement.GetProperty("iconicName").GetString()?.Trim() ?? "";
                    var skillTitle = skillElement.GetProperty("title").GetString()?.Trim() ?? "";
                    var description = skillElement.TryGetProperty("description", out var descProp) && descProp.ValueKind != JsonValueKind.Null
                        ? descProp.GetString()?.Trim()
                        : null;
                    var orderInTree = skillElement.GetProperty("orderInTree").GetInt32();
                    var stage = skillElement.TryGetProperty("stage", out var stageProp) && stageProp.ValueKind == JsonValueKind.String
                        ? stageProp.GetString()?.Trim()
                        : null;
                    UpsertSkill(skillIconicName, skillTitle, description, orderInTree, stage, existingSkills, skillsState);
                }
                catch (Exception exception)
                {
                    errors.Add($"Skill {skillIndex} ('{skillIconicName}'): {exception.Message}");
                    continue;
                }

                if (!existingSkills.TryGetValue(skillIconicName, out var skill))
                    continue;

                if (!skillElement.TryGetProperty("topics", out var topicsElement) || topicsElement.ValueKind != JsonValueKind.Array)
                    continue;

                foreach (var (topicElement, topicIndex) in topicsElement.EnumerateArray().Select((element, index) => (element, index + 1)))
                {
                    var topicIconicName = "";
                    try
                    {
                        topicIconicName = topicElement.GetProperty("iconicName").GetString()?.Trim() ?? "";
                        var topicTitle = topicElement.GetProperty("title").GetString()?.Trim() ?? "";
                        var orderInSkill = topicElement.GetProperty("orderInSkill").GetInt32();

                        var clashing = existingTopics.FirstOrDefault(topic => topic.IconicName == topicIconicName);
                        if (clashing is not null && clashing.SkillId != skill.Id)
                        {
                            errors.Add($"Skill '{skillIconicName}', topic {topicIndex} ('{topicIconicName}'): iconicName already belongs to another skill.");
                            continue;
                        }

                        UpsertTopic(skill.Id, topicIconicName, topicTitle, orderInSkill, existingTopics, topicsState);
                    }
                    catch (Exception exception)
                    {
                        errors.Add($"Skill '{skillIconicName}', topic {topicIndex} ('{topicIconicName}'): {exception.Message}");
                        continue;
                    }

                    var topic = existingTopics.FirstOrDefault(candidate => candidate.IconicName == topicIconicName);
                    if (topic is null)
                        continue;

                    if (!topicElement.TryGetProperty("lessons", out var lessonsElement) || lessonsElement.ValueKind != JsonValueKind.Array)
                        continue;

                    foreach (var (lessonElement, lessonIndex) in lessonsElement.EnumerateArray().Select((element, index) => (element, index + 1)))
                    {
                        Lesson lesson;
                        var lessonTitle = "";
                        try
                        {
                            lessonTitle = lessonElement.GetProperty("title").GetString()?.Trim() ?? "";
                            if (string.IsNullOrWhiteSpace(lessonTitle))
                                throw new InvalidOperationException("title is empty.");
                            var orderInTopic = lessonElement.GetProperty("orderInTopic").GetInt32();
                            lesson = UpsertLesson(topic.Id, lessonTitle, orderInTopic, allLessons, lessonsState);
                        }
                        catch (Exception exception)
                        {
                            errors.Add($"Topic '{topicIconicName}', lesson {lessonIndex} ('{lessonTitle}'): {exception.Message}");
                            continue;
                        }

                        if (!lessonElement.TryGetProperty("exercises", out var exercisesElement) || exercisesElement.ValueKind != JsonValueKind.Array)
                            continue;

                        foreach (var (exerciseElement, exerciseIndex) in exercisesElement.EnumerateArray().Select((element, index) => (element, index + 1)))
                        {
                            try
                            {
                                var exerciseType = exerciseElement.GetProperty("type").GetString()?.Trim() ?? "";
                                var orderInLesson = exerciseElement.GetProperty("orderInLesson").GetInt32();
                                var contentElement = exerciseElement.GetProperty("content");
                                var customAiPrompt = exerciseElement.TryGetProperty("customAiPrompt", out var promptProp) && promptProp.ValueKind != JsonValueKind.Null
                                    ? promptProp.GetString()
                                    : null;

                                var contentErrors = ExerciseContentValidator.Validate(exerciseType, contentElement);
                                if (contentErrors.Count > 0)
                                {
                                    errors.Add($"Lesson '{lessonTitle}', exercise {exerciseIndex} ({exerciseType}): {string.Join(" ", contentErrors)}");
                                    continue;
                                }

                                UpsertExercise(lesson, exerciseType, orderInLesson, contentElement.GetRawText(), customAiPrompt, allExercises, lessonsState);
                            }
                            catch (Exception exception)
                            {
                                errors.Add($"Lesson '{lessonTitle}', exercise {exerciseIndex}: {exception.Message}");
                            }
                        }
                    }
                }
            }
        }
        catch (JsonException exception) { return BadRequest(new { message = $"JSON parse error: {exception.Message}" }); }

        await database.SaveChangesAsync(cancellationToken);

        var actorId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        logger.LogInformation(
            "Bundle seeder import: Skills={SkillsCreated}/{SkillsUpdated} Topics={TopicsCreated}/{TopicsUpdated} Lessons={LessonsCreated}/{LessonsUpdated} Exercises={ExercisesCreated}/{ExercisesUpdated} Errors={ErrorCount} by ActorId={ActorId}",
            skillsState.SkillsCreated, skillsState.SkillsUpdated,
            topicsState.TopicsCreated, topicsState.TopicsUpdated,
            lessonsState.LessonsCreated, lessonsState.LessonsUpdated,
            lessonsState.ExercisesCreated, lessonsState.ExercisesUpdated,
            errors.Count, actorId);

        return Ok(new BundleImportResultDto(
            skillsState.SkillsCreated, skillsState.SkillsUpdated,
            topicsState.TopicsCreated, topicsState.TopicsUpdated,
            lessonsState.LessonsCreated, lessonsState.LessonsUpdated,
            lessonsState.ExercisesCreated, lessonsState.ExercisesUpdated,
            errors));
    }

    [HttpGet("admin/seeder/skills/export")]
    public async Task<ActionResult<IReadOnlyList<SkillExportDto>>> ExportSkills(CancellationToken cancellationToken = default)
    {
        var skills = await database.Skills.AsNoTracking()
            .OrderBy(skill => skill.OrderInTree).ThenBy(skill => skill.IconicName)
            .ToListAsync(cancellationToken);

        var payload = skills
            .Select(skill => new SkillExportDto(skill.IconicName, skill.Title, skill.Description, skill.OrderInTree, skill.Stage))
            .ToList();

        LogExport("skills", payload.Count);
        return Ok(payload);
    }

    [HttpGet("admin/seeder/topics/export")]
    public async Task<ActionResult<IReadOnlyList<TopicExportDto>>> ExportTopics(CancellationToken cancellationToken = default)
    {
        var skillIconicById = await database.Skills.AsNoTracking()
            .ToDictionaryAsync(skill => skill.Id, skill => skill.IconicName, cancellationToken);
        var topics = await database.Topics.AsNoTracking().ToListAsync(cancellationToken);

        var payload = topics
            .OrderBy(topic => skillIconicById.GetValueOrDefault(topic.SkillId, "")).ThenBy(topic => topic.OrderInSkill)
            .Select(topic => new TopicExportDto(
                skillIconicById.GetValueOrDefault(topic.SkillId, ""), topic.IconicName, topic.Title, topic.OrderInSkill))
            .ToList();

        LogExport("topics", payload.Count);
        return Ok(payload);
    }

    [HttpGet("admin/seeder/lessons/export")]
    public async Task<ActionResult<IReadOnlyList<LessonExportDto>>> ExportLessons(CancellationToken cancellationToken = default)
    {
        var topicIconicById = await database.Topics.AsNoTracking()
            .ToDictionaryAsync(topic => topic.Id, topic => topic.IconicName, cancellationToken);
        var lessons = await database.Lessons.AsNoTracking().ToListAsync(cancellationToken);
        var exercisesByLesson = await LoadExercisesByLessonAsync(cancellationToken);

        var payload = lessons
            .OrderBy(lesson => topicIconicById.GetValueOrDefault(lesson.TopicId, "")).ThenBy(lesson => lesson.OrderInTopic)
            .Select(lesson => new LessonExportDto(
                topicIconicById.GetValueOrDefault(lesson.TopicId, ""),
                lesson.Title, lesson.OrderInTopic,
                BuildExercises(exercisesByLesson, lesson.Id)))
            .ToList();

        LogExport("lessons", payload.Count);
        return Ok(payload);
    }

    [HttpGet("admin/seeder/bundle/export")]
    public async Task<ActionResult<BundleExportDto>> ExportBundle(CancellationToken cancellationToken = default)
    {
        var skills = await database.Skills.AsNoTracking()
            .OrderBy(skill => skill.OrderInTree).ThenBy(skill => skill.IconicName)
            .ToListAsync(cancellationToken);
        var topics = await database.Topics.AsNoTracking().ToListAsync(cancellationToken);
        var lessons = await database.Lessons.AsNoTracking().ToListAsync(cancellationToken);
        var exercisesByLesson = await LoadExercisesByLessonAsync(cancellationToken);

        var topicsBySkill = topics.GroupBy(topic => topic.SkillId)
            .ToDictionary(group => group.Key, group => group.OrderBy(topic => topic.OrderInSkill).ToList());
        var lessonsByTopic = lessons.GroupBy(lesson => lesson.TopicId)
            .ToDictionary(group => group.Key, group => group.OrderBy(lesson => lesson.OrderInTopic).ToList());

        var skillDtos = skills.Select(skill => new BundleSkillDto(
            skill.IconicName, skill.Title, skill.Description, skill.OrderInTree, skill.Stage,
            (topicsBySkill.TryGetValue(skill.Id, out var skillTopics) ? skillTopics : [])
                .Select(topic => new BundleTopicDto(
                    topic.IconicName, topic.Title, topic.OrderInSkill,
                    (lessonsByTopic.TryGetValue(topic.Id, out var topicLessons) ? topicLessons : [])
                        .Select(lesson => new BundleLessonDto(
                            lesson.Title, lesson.OrderInTopic, BuildExercises(exercisesByLesson, lesson.Id)))
                        .ToList()))
                .ToList()))
            .ToList();

        LogExport("bundle", skillDtos.Count);
        return Ok(new BundleExportDto(skillDtos));
    }

    private async Task<Dictionary<Guid, List<Exercise>>> LoadExercisesByLessonAsync(CancellationToken cancellationToken)
    {
        var exercises = await database.Exercises.AsNoTracking().ToListAsync(cancellationToken);
        return exercises
            .GroupBy(exercise => exercise.LessonId)
            .ToDictionary(group => group.Key, group => group.OrderBy(exercise => exercise.OrderInLesson).ToList());
    }

    private static IReadOnlyList<ExerciseSeedDto> BuildExercises(
        Dictionary<Guid, List<Exercise>> exercisesByLesson, Guid lessonId) =>
        (exercisesByLesson.TryGetValue(lessonId, out var list) ? list : [])
            .Select(exercise => new ExerciseSeedDto(
                exercise.Type, exercise.OrderInLesson, ParseContent(exercise.SerializedContent), exercise.CustomAiPrompt))
            .ToList();

    private static JsonNode? ParseContent(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        try { return JsonNode.Parse(raw); }
        catch (JsonException) { return null; }
    }

    private void LogExport(string kind, int count) =>
        logger.LogInformation(
            "Seeder export ({Kind}): {Count} root items by ActorId={ActorId}",
            kind, count, User.FindFirstValue(ClaimTypes.NameIdentifier));

    private Lesson UpsertLesson(Guid topicId, string title, int orderInTopic,
        List<Lesson> existingLessons, LessonsImportState state)
    {
        var existing = existingLessons.FirstOrDefault(lesson => lesson.TopicId == topicId && lesson.Title == title);
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
        database.Lessons.Add(lesson);
        existingLessons.Add(lesson);
        state.LessonsCreated++;
        return lesson;
    }

    private void UpsertExercise(Lesson lesson, string type, int orderInLesson, string contentJson, string? customAiPrompt,
        List<Exercise> existingExercises, LessonsImportState state)
    {
        var now = DateTime.UtcNow;
        var existing = existingExercises.FirstOrDefault(exercise => exercise.LessonId == lesson.Id && exercise.OrderInLesson == orderInLesson);
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
            database.Exercises.Add(exercise);
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
