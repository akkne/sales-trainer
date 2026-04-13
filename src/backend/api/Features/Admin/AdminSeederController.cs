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
public record LessonsImportResultDto(int LessonsCreated, int LessonsUpdated, int ExercisesCreated, int ExercisesUpdated, List<string> Errors);

[ApiController]
[Authorize(Policy = "RequireAdmin")]
public class AdminSeederController(AppDbContext db, ILogger<AdminSeederController> logger) : ControllerBase
{
    // ---- Skills import (CSV or JSON) ----

    [HttpPost("admin/seeder/skills")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<ActionResult<SkillsImportResultDto>> ImportSkills(IFormFile file)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { message = "File is required." });

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (ext != ".csv" && ext != ".json")
            return BadRequest(new { message = "Only .csv and .json files are accepted." });

        var existingSkills = await db.Skills.ToDictionaryAsync(s => s.Slug);
        var state = new SkillsImportState();

        if (ext == ".csv")
        {
            List<Dictionary<string, string>> rows;
            try { rows = ParseCsv(file.OpenReadStream()); }
            catch (Exception ex) { return BadRequest(new { message = $"CSV parse error: {ex.Message}" }); }

            if (rows.Count == 0) return BadRequest(new { message = "CSV has no data rows." });

            var required = new[] { "slug", "title", "icon_name", "sort_order", "sales_types" };
            var missing = required.Where(c => !rows[0].ContainsKey(c)).ToList();
            if (missing.Count > 0)
                return BadRequest(new { message = $"Missing columns: {string.Join(", ", missing)}" });

            foreach (var (row, index) in rows.Select((r, i) => (r, i + 2)))
            {
                try
                {
                    var salesTypes = row["sales_types"].Split('|',
                        StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    UpsertSkill(row["slug"].Trim(), row["title"].Trim(), row["icon_name"].Trim(),
                        ParseInt(row["sort_order"], "sort_order"), salesTypes, existingSkills, state);
                }
                catch (Exception ex) { state.Errors.Add($"Row {index}: {ex.Message}"); }
            }
        }
        else
        {
            try
            {
                using var doc = await JsonDocument.ParseAsync(file.OpenReadStream());
                if (doc.RootElement.ValueKind != JsonValueKind.Array)
                    return BadRequest(new { message = "JSON must be an array of skill objects." });

                var prerequisiteMap = new List<(string slug, string iconName)>();

                foreach (var (el, index) in doc.RootElement.EnumerateArray().Select((e, i) => (e, i + 1)))
                {
                    try
                    {
                        var slug = el.GetProperty("slug").GetString()?.Trim() ?? "";
                        var title = el.GetProperty("title").GetString()?.Trim() ?? "";
                        var iconName = el.GetProperty("iconName").GetString()?.Trim() ?? "";
                        var sortOrder = el.GetProperty("sortOrder").GetInt32();
                        var salesTypes = el.GetProperty("applicableSalesTypes").EnumerateArray()
                            .Select(e => e.GetString()?.Trim() ?? "")
                            .Where(s => s != "").ToArray();
                        UpsertSkill(slug, title, iconName, sortOrder, salesTypes, existingSkills, state);

                        if (el.TryGetProperty("prerequisiteSkillIcon", out var prereqProp)
                            && prereqProp.ValueKind != JsonValueKind.Null)
                        {
                            var prereqIcon = prereqProp.GetString()?.Trim();
                            if (!string.IsNullOrEmpty(prereqIcon))
                                prerequisiteMap.Add((slug, prereqIcon));
                        }
                        else if (existingSkills.TryGetValue(slug, out var existingSkill))
                        {
                            existingSkill.PrerequisiteSkillId = null;
                        }
                    }
                    catch (Exception ex) { state.Errors.Add($"Item {index}: {ex.Message}"); }
                }

                if (prerequisiteMap.Count > 0)
                {
                    var iconToSkill = existingSkills.Values
                        .GroupBy(s => s.IconName)
                        .ToDictionary(g => g.Key, g => g.First());

                    foreach (var (slug, prereqIcon) in prerequisiteMap)
                    {
                        if (existingSkills.TryGetValue(slug, out var skill)
                            && iconToSkill.TryGetValue(prereqIcon, out var prereqSkill))
                            skill.PrerequisiteSkillId = prereqSkill.Id;
                        else
                            state.Errors.Add($"Cannot resolve prerequisiteSkillIcon '{prereqIcon}' for skill '{slug}'.");
                    }
                }
            }
            catch (JsonException ex) { return BadRequest(new { message = $"JSON parse error: {ex.Message}" }); }
        }

        await db.SaveChangesAsync();

        var actorId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        logger.LogInformation(
            "Skills seeder import: SkillsCreated={SC} SkillsUpdated={SU} Errors={ErrCount} by ActorId={ActorId}",
            state.SkillsCreated, state.SkillsUpdated, state.Errors.Count, actorId);

        return Ok(new SkillsImportResultDto(state.SkillsCreated, state.SkillsUpdated, state.Errors));
    }

    private void UpsertSkill(string slug, string title, string iconName, int sortOrder, string[] salesTypes,
        Dictionary<string, Skill> existingSkills, SkillsImportState state)
    {
        if (string.IsNullOrWhiteSpace(slug)) throw new InvalidOperationException("slug is empty.");

        if (existingSkills.TryGetValue(slug, out var found))
        {
            found.Title = title;
            found.IconName = iconName;
            found.SortOrder = sortOrder;
            found.ApplicableSalesTypes = salesTypes;
            found.PrerequisiteSkillId = null; // will be resolved in second pass if applicable
            if (state.UpdatedSlugs.Add(slug)) state.SkillsUpdated++;
        }
        else
        {
            var skill = new Skill
            {
                Id = Guid.NewGuid(),
                Slug = slug,
                Title = title,
                IconName = iconName,
                SortOrder = sortOrder,
                ApplicableSalesTypes = salesTypes
            };
            db.Skills.Add(skill);
            existingSkills[slug] = skill;
            if (state.CreatedSlugs.Add(slug)) state.SkillsCreated++;
        }
    }

    // ---- Lessons + Exercises import (CSV or JSON, skillIcons list embedded in each row/item) ----
    // CSV columns: skill_icons (pipe-separated), lesson_title, lesson_sort_order, lesson_difficulty,
    //              lesson_xp, exercise_type, exercise_sort_order, exercise_content_json
    // JSON: [{ "skillIcons": ["cold-calls","objection"], "title": "...", "sortOrder": 1,
    //          "xpReward": 50, "difficultyLevel": 1, "exercises": [...] }]

    [HttpPost("admin/seeder/lessons")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<ActionResult<LessonsImportResultDto>> ImportLessons(IFormFile file)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { message = "File is required." });

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (ext != ".csv" && ext != ".json")
            return BadRequest(new { message = "Only .csv and .json files are accepted." });

        var skillsByIcon = await db.Skills.ToDictionaryAsync(s => s.IconName);
        var allLessons = await db.Lessons.ToListAsync();
        var allExercises = await db.Exercises.ToListAsync();

        var state = new LessonsImportState();

        if (ext == ".csv")
        {
            List<Dictionary<string, string>> rows;
            try { rows = ParseCsv(file.OpenReadStream()); }
            catch (Exception ex) { return BadRequest(new { message = $"CSV parse error: {ex.Message}" }); }

            if (rows.Count == 0) return BadRequest(new { message = "CSV has no data rows." });

            var required = new[] { "skill_icons", "lesson_title", "lesson_sort_order", "lesson_difficulty", "lesson_xp",
                "exercise_type", "exercise_sort_order", "exercise_content_json" };
            var missing = required.Where(c => !rows[0].ContainsKey(c)).ToList();
            if (missing.Count > 0)
                return BadRequest(new { message = $"Missing columns: {string.Join(", ", missing)}" });

            foreach (var (row, index) in rows.Select((r, i) => (r, i + 2)))
            {
                try
                {
                    var icons = row["skill_icons"].Split('|',
                        StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    if (icons.Length == 0)
                    {
                        state.Errors.Add($"Row {index}: skill_icons is empty.");
                        continue;
                    }
                    foreach (var icon in icons)
                    {
                        if (!skillsByIcon.TryGetValue(icon, out var skill))
                        {
                            state.Errors.Add($"Row {index}: skill with icon '{icon}' not found.");
                            continue;
                        }
                        ProcessLessonRow(row, skill.Id, allLessons, allExercises, state);
                    }
                }
                catch (Exception ex) { state.Errors.Add($"Row {index}: {ex.Message}"); }
            }
        }
        else
        {
            try
            {
                using var doc = await JsonDocument.ParseAsync(file.OpenReadStream());
                if (doc.RootElement.ValueKind != JsonValueKind.Array)
                    return BadRequest(new { message = "JSON must be an array of lesson objects." });

                foreach (var (lessonEl, lessonIdx) in doc.RootElement.EnumerateArray().Select((e, i) => (e, i + 1)))
                {
                    try
                    {
                        if (!lessonEl.TryGetProperty("skillIcons", out var iconsEl)
                            || iconsEl.ValueKind != JsonValueKind.Array)
                        {
                            state.Errors.Add($"Item {lessonIdx}: 'skillIcons' array is required.");
                            continue;
                        }

                        var icons = iconsEl.EnumerateArray()
                            .Select(e => e.GetString()?.Trim() ?? "")
                            .Where(s => s.Length > 0)
                            .ToList();

                        if (icons.Count == 0)
                        {
                            state.Errors.Add($"Item {lessonIdx}: 'skillIcons' is empty.");
                            continue;
                        }

                        var lessonTitle = lessonEl.GetProperty("title").GetString()?.Trim() ?? "";
                        var lessonSortOrder = lessonEl.GetProperty("sortOrder").GetInt32();
                        var difficulty = lessonEl.TryGetProperty("difficultyLevel", out var diffProp) ? diffProp.GetInt32() : 1;
                        var xp = lessonEl.GetProperty("xpReward").GetInt32();

                        lessonEl.TryGetProperty("exercises", out var exercisesEl);

                        foreach (var icon in icons)
                        {
                            if (!skillsByIcon.TryGetValue(icon, out var skill))
                            {
                                state.Errors.Add($"Item {lessonIdx}: skill with icon '{icon}' not found.");
                                continue;
                            }

                            var lesson = UpsertLesson(skill.Id, lessonTitle, lessonSortOrder, difficulty, xp, allLessons, state);

                            if (exercisesEl.ValueKind == JsonValueKind.Array)
                            {
                                foreach (var (exEl, exIdx) in exercisesEl.EnumerateArray().Select((e, i) => (e, i + 1)))
                                {
                                    try
                                    {
                                        var exType = exEl.GetProperty("type").GetString()?.Trim() ?? "";
                                        var exSortOrder = exEl.GetProperty("sortOrder").GetInt32();
                                        var contentJson = exEl.GetProperty("content").GetRawText();
                                        UpsertExercise(lesson, exType, exSortOrder, contentJson, allExercises, state);
                                    }
                                    catch (Exception ex)
                                    {
                                        state.Errors.Add($"Item {lessonIdx} ({icon}), exercise {exIdx}: {ex.Message}");
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex) { state.Errors.Add($"Item {lessonIdx}: {ex.Message}"); }
                }
            }
            catch (JsonException ex) { return BadRequest(new { message = $"JSON parse error: {ex.Message}" }); }
        }

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

    private void ProcessLessonRow(
        Dictionary<string, string> row,
        Guid skillId,
        List<Lesson> existingLessons,
        List<Exercise> existingExercises,
        LessonsImportState state)
    {
        var lessonTitle = row["lesson_title"].Trim();
        if (string.IsNullOrWhiteSpace(lessonTitle)) throw new InvalidOperationException("lesson_title is empty.");

        var lesson = UpsertLesson(skillId, lessonTitle,
            ParseInt(row["lesson_sort_order"], "lesson_sort_order"),
            ParseInt(row["lesson_difficulty"], "lesson_difficulty"),
            ParseInt(row["lesson_xp"], "lesson_xp"),
            existingLessons, state);

        var exerciseType = row["exercise_type"].Trim();
        if (string.IsNullOrWhiteSpace(exerciseType)) throw new InvalidOperationException("exercise_type is empty.");

        var exerciseSortOrder = ParseInt(row["exercise_sort_order"], "exercise_sort_order");
        var contentJson = row["exercise_content_json"].Trim();

        try { JsonDocument.Parse(contentJson); }
        catch { throw new InvalidOperationException("exercise_content_json is not valid JSON."); }

        UpsertExercise(lesson, exerciseType, exerciseSortOrder, contentJson, existingExercises, state);
    }

    private Lesson UpsertLesson(Guid skillId, string title, int sortOrder, int difficulty, int xpReward,
        List<Lesson> existingLessons, LessonsImportState state)
    {
        var lessonKey = $"{skillId}::{title}";
        var existing = existingLessons.FirstOrDefault(l => l.SkillId == skillId && l.Title == title);

        if (existing is not null)
        {
            existing.SortOrder = sortOrder;
            existing.DifficultyLevel = difficulty;
            existing.XpReward = xpReward;
            if (state.UpdatedLessonKeys.Add(lessonKey)) state.LessonsUpdated++;
            return existing;
        }

        var lesson = new Lesson
        {
            Id = Guid.NewGuid(),
            SkillId = skillId,
            Title = title,
            SortOrder = sortOrder,
            DifficultyLevel = difficulty,
            XpReward = xpReward
        };
        db.Lessons.Add(lesson);
        existingLessons.Add(lesson);
        if (state.CreatedLessonKeys.Add(lessonKey)) state.LessonsCreated++;
        return lesson;
    }

    private void UpsertExercise(Lesson lesson, string type, int sortOrder, string contentJson,
        List<Exercise> existingExercises, LessonsImportState state)
    {
        var existing = existingExercises.FirstOrDefault(e => e.LessonId == lesson.Id && e.SortOrder == sortOrder);
        if (existing is not null)
        {
            existing.Type = type;
            existing.SerializedContent = contentJson;
            state.ExercisesUpdated++;
        }
        else
        {
            var exercise = new Exercise
            {
                Id = Guid.NewGuid(),
                LessonId = lesson.Id,
                Type = type,
                SortOrder = sortOrder,
                SerializedContent = contentJson
            };
            db.Exercises.Add(exercise);
            existingExercises.Add(exercise);
            state.ExercisesCreated++;
        }
    }

    // ---- CSV parser ----

    private static List<Dictionary<string, string>> ParseCsv(Stream stream)
    {
        var rows = new List<Dictionary<string, string>>();
        using var reader = new StreamReader(stream, Encoding.UTF8);

        var headerLine = reader.ReadLine()
            ?? throw new InvalidOperationException("CSV is empty.");
        var headers = SplitLine(headerLine);

        string? line;
        var lineNumber = 1;
        while ((line = reader.ReadLine()) is not null)
        {
            lineNumber++;
            if (string.IsNullOrWhiteSpace(line)) continue;

            var values = SplitLine(line);
            if (values.Length != headers.Length)
                throw new InvalidOperationException(
                    $"Line {lineNumber} has {values.Length} fields, expected {headers.Length}.");

            var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < headers.Length; i++)
                row[headers[i].Trim()] = values[i];
            rows.Add(row);
        }

        return rows;
    }

    private static string[] SplitLine(string line)
    {
        var fields = new List<string>();
        var i = 0;
        while (i <= line.Length)
        {
            if (i == line.Length) { fields.Add(string.Empty); break; }

            if (line[i] == '"')
            {
                i++;
                var sb = new StringBuilder();
                while (i < line.Length)
                {
                    if (line[i] == '"')
                    {
                        if (i + 1 < line.Length && line[i + 1] == '"') { sb.Append('"'); i += 2; }
                        else { i++; break; }
                    }
                    else { sb.Append(line[i++]); }
                }
                fields.Add(sb.ToString());
                if (i < line.Length && line[i] == ',') i++;
                else break;
            }
            else
            {
                var end = line.IndexOf(',', i);
                if (end == -1) { fields.Add(line[i..]); break; }
                fields.Add(line[i..end]);
                i = end + 1;
            }
        }
        return [.. fields];
    }

    private static int ParseInt(string value, string fieldName)
    {
        if (!int.TryParse(value.Trim(), out var result))
            throw new InvalidOperationException($"Field '{fieldName}' must be an integer, got '{value}'.");
        return result;
    }

    // ---- Import state ----

    private sealed class SkillsImportState
    {
        public int SkillsCreated { get; set; }
        public int SkillsUpdated { get; set; }
        public List<string> Errors { get; } = [];
        public HashSet<string> CreatedSlugs { get; } = [];
        public HashSet<string> UpdatedSlugs { get; } = [];
    }

    private sealed class LessonsImportState
    {
        public int LessonsCreated { get; set; }
        public int LessonsUpdated { get; set; }
        public int ExercisesCreated { get; set; }
        public int ExercisesUpdated { get; set; }
        public List<string> Errors { get; } = [];
        public HashSet<string> CreatedLessonKeys { get; } = [];
        public HashSet<string> UpdatedLessonKeys { get; } = [];
    }
}
