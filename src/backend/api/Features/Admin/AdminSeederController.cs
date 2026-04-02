using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SalesTrainer.Api.Features.Lessons;
using SalesTrainer.Api.Features.SkillTree;
using SalesTrainer.Api.Infrastructure.Data;

namespace SalesTrainer.Api.Features.Admin;

public record SeederImportResultDto(
    int SkillsCreated,
    int SkillsUpdated,
    int LessonsCreated,
    int LessonsUpdated,
    int ExercisesCreated,
    int ExercisesUpdated,
    List<string> Errors
);

[ApiController]
[Authorize(Policy = "RequireAdmin")]
public class AdminSeederController(AppDbContext db, ILogger<AdminSeederController> logger) : ControllerBase
{
    private static readonly string[] RequiredColumns =
    [
        "skill_slug", "skill_title", "skill_icon", "skill_sort_order", "skill_sales_types",
        "lesson_title", "lesson_sort_order", "lesson_difficulty", "lesson_xp",
        "exercise_type", "exercise_sort_order", "exercise_content_json"
    ];

    [HttpPost("admin/seeder/csv")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<ActionResult<SeederImportResultDto>> ImportCsv(IFormFile file)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { message = "CSV file is required." });

        if (!file.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { message = "Only .csv files are accepted." });

        List<Dictionary<string, string>> rows;
        try
        {
            rows = ParseCsv(file.OpenReadStream());
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = $"CSV parse error: {ex.Message}" });
        }

        if (rows.Count == 0)
            return BadRequest(new { message = "CSV has no data rows." });

        var firstRowKeys = rows[0].Keys.ToHashSet();
        var missingCols = RequiredColumns.Where(c => !firstRowKeys.Contains(c)).ToList();
        if (missingCols.Count > 0)
            return BadRequest(new { message = $"Missing columns: {string.Join(", ", missingCols)}" });

        // Load existing data into memory — single round-trip per entity type
        var existingSkills = await db.Skills.ToDictionaryAsync(s => s.Slug);
        var existingLessons = await db.Lessons.ToListAsync();
        var existingExercises = await db.Exercises.ToListAsync();

        var state = new ImportState();

        foreach (var (row, index) in rows.Select((r, i) => (r, i + 2))) // +2 = 1-based + header
        {
            try
            {
                ProcessRow(row, existingSkills, existingLessons, existingExercises, state);
            }
            catch (Exception ex)
            {
                state.Errors.Add($"Row {index}: {ex.Message}");
            }
        }

        await db.SaveChangesAsync();

        var actorId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        logger.LogInformation(
            "CSV seeder import: SkillsCreated={SC} SkillsUpdated={SU} LessonsCreated={LC} LessonsUpdated={LU} ExercisesCreated={EC} ExercisesUpdated={EU} Errors={ErrCount} by ActorId={ActorId}",
            state.SkillsCreated, state.SkillsUpdated,
            state.LessonsCreated, state.LessonsUpdated,
            state.ExercisesCreated, state.ExercisesUpdated,
            state.Errors.Count, actorId);

        return Ok(new SeederImportResultDto(
            state.SkillsCreated, state.SkillsUpdated,
            state.LessonsCreated, state.LessonsUpdated,
            state.ExercisesCreated, state.ExercisesUpdated,
            state.Errors));
    }

    private void ProcessRow(
        Dictionary<string, string> row,
        Dictionary<string, Skill> existingSkills,
        List<Lesson> existingLessons,
        List<Exercise> existingExercises,
        ImportState state)
    {
        // ---- Skill ----
        var slug = row["skill_slug"].Trim();
        if (string.IsNullOrWhiteSpace(slug))
            throw new InvalidOperationException("skill_slug is empty.");

        var salesTypes = row["skill_sales_types"]
            .Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        Skill skill;
        if (existingSkills.TryGetValue(slug, out var found))
        {
            found.Title = row["skill_title"].Trim();
            found.IconName = row["skill_icon"].Trim();
            found.SortOrder = ParseInt(row["skill_sort_order"], "skill_sort_order");
            found.ApplicableSalesTypes = salesTypes;
            skill = found;
            if (state.UpdatedSkillSlugs.Add(slug)) state.SkillsUpdated++;
        }
        else
        {
            skill = new Skill
            {
                Id = Guid.NewGuid(),
                Slug = slug,
                Title = row["skill_title"].Trim(),
                IconName = row["skill_icon"].Trim(),
                SortOrder = ParseInt(row["skill_sort_order"], "skill_sort_order"),
                ApplicableSalesTypes = salesTypes
            };
            db.Skills.Add(skill);
            existingSkills[slug] = skill;
            if (state.CreatedSkillSlugs.Add(slug)) state.SkillsCreated++;
        }

        // ---- Lesson ----
        var lessonTitle = row["lesson_title"].Trim();
        if (string.IsNullOrWhiteSpace(lessonTitle))
            throw new InvalidOperationException("lesson_title is empty.");

        var lessonSortOrder = ParseInt(row["lesson_sort_order"], "lesson_sort_order");
        var difficulty = ParseInt(row["lesson_difficulty"], "lesson_difficulty");
        var xpReward = ParseInt(row["lesson_xp"], "lesson_xp");
        var lessonKey = $"{slug}::{lessonTitle}";

        var existingLesson = existingLessons
            .FirstOrDefault(l => l.SkillId == skill.Id && l.Title == lessonTitle);

        Lesson lesson;
        if (existingLesson is not null)
        {
            existingLesson.SortOrder = lessonSortOrder;
            existingLesson.DifficultyLevel = difficulty;
            existingLesson.XpReward = xpReward;
            lesson = existingLesson;
            if (state.UpdatedLessonKeys.Add(lessonKey)) state.LessonsUpdated++;
        }
        else
        {
            lesson = new Lesson
            {
                Id = Guid.NewGuid(),
                SkillId = skill.Id,
                Title = lessonTitle,
                SortOrder = lessonSortOrder,
                DifficultyLevel = difficulty,
                XpReward = xpReward
            };
            db.Lessons.Add(lesson);
            existingLessons.Add(lesson);
            if (state.CreatedLessonKeys.Add(lessonKey)) state.LessonsCreated++;
        }

        // ---- Exercise ----
        var exerciseType = row["exercise_type"].Trim();
        if (string.IsNullOrWhiteSpace(exerciseType))
            throw new InvalidOperationException("exercise_type is empty.");

        var exerciseSortOrder = ParseInt(row["exercise_sort_order"], "exercise_sort_order");
        var contentJson = row["exercise_content_json"].Trim();

        // Validate JSON before saving
        try { JsonDocument.Parse(contentJson); }
        catch { throw new InvalidOperationException("exercise_content_json is not valid JSON."); }

        var existingExercise = existingExercises
            .FirstOrDefault(e => e.LessonId == lesson.Id && e.SortOrder == exerciseSortOrder);

        if (existingExercise is not null)
        {
            existingExercise.Type = exerciseType;
            existingExercise.SerializedContent = contentJson;
            state.ExercisesUpdated++;
        }
        else
        {
            var newExercise = new Exercise
            {
                Id = Guid.NewGuid(),
                LessonId = lesson.Id,
                Type = exerciseType,
                SortOrder = exerciseSortOrder,
                SerializedContent = contentJson
            };
            db.Exercises.Add(newExercise);
            existingExercises.Add(newExercise);
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
            if (i == line.Length)
            {
                // trailing comma produced empty last field
                fields.Add(string.Empty);
                break;
            }

            if (line[i] == '"')
            {
                i++; // skip opening quote
                var sb = new StringBuilder();
                while (i < line.Length)
                {
                    if (line[i] == '"')
                    {
                        if (i + 1 < line.Length && line[i + 1] == '"')
                        {
                            sb.Append('"');
                            i += 2;
                        }
                        else
                        {
                            i++; // skip closing quote
                            break;
                        }
                    }
                    else
                    {
                        sb.Append(line[i++]);
                    }
                }
                fields.Add(sb.ToString());
                if (i < line.Length && line[i] == ',') i++;
                else break;
            }
            else
            {
                var end = line.IndexOf(',', i);
                if (end == -1)
                {
                    fields.Add(line[i..]);
                    break;
                }
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

    // ---- Import state tracker ----

    private sealed class ImportState
    {
        public int SkillsCreated { get; set; }
        public int SkillsUpdated { get; set; }
        public int LessonsCreated { get; set; }
        public int LessonsUpdated { get; set; }
        public int ExercisesCreated { get; set; }
        public int ExercisesUpdated { get; set; }
        public List<string> Errors { get; } = [];
        public HashSet<string> CreatedSkillSlugs { get; } = [];
        public HashSet<string> UpdatedSkillSlugs { get; } = [];
        public HashSet<string> CreatedLessonKeys { get; } = [];
        public HashSet<string> UpdatedLessonKeys { get; } = [];
    }
}
