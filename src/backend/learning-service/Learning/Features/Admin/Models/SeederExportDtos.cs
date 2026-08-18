using System.Text.Json.Nodes;

namespace Sellevate.Learning.Features.Admin;

/// <summary>
/// Export shapes for learning content, mirroring the seeder import format so an
/// exported file re-imports verbatim through the matching <c>POST /admin/seeder/*</c>
/// endpoint. Exercise <c>content</c> is emitted as a JSON object (not a string).
/// </summary>
public sealed record ExerciseSeedDto(
    string Type,
    int OrderInLesson,
    JsonNode? Content,
    string? CustomAiPrompt);

/// <summary>
/// One of the three flat export shapes, each re-importable through its own
/// <c>POST /admin/seeder/{skills,topics,lessons}</c> route. Parents are referenced by iconic name
/// rather than by id, because ids differ between environments and the file has to survive the trip.
/// </summary>
public sealed record SkillExportDto(
    string IconicName,
    string Title,
    string? Description,
    int OrderInTree,
    string Stage);

public sealed record TopicExportDto(
    string SkillIconicName,
    string IconicName,
    string Title,
    int OrderInSkill);

public sealed record LessonExportDto(
    string TopicIconicName,
    string Title,
    int OrderInTopic,
    IReadOnlyList<ExerciseSeedDto> Exercises);

/// <summary>
/// The nested export, which round-trips through <c>POST /admin/seeder/bundle</c>. Same content as the
/// three flat shapes above, carried as one tree so a whole curriculum moves in a single file.
/// </summary>
public sealed record BundleExportDto(IReadOnlyList<BundleSkillDto> Skills);

public sealed record BundleSkillDto(
    string IconicName,
    string Title,
    string? Description,
    int OrderInTree,
    string Stage,
    IReadOnlyList<BundleTopicDto> Topics);

public sealed record BundleTopicDto(
    string IconicName,
    string Title,
    int OrderInSkill,
    IReadOnlyList<BundleLessonDto> Lessons);

public sealed record BundleLessonDto(
    string Title,
    int OrderInTopic,
    IReadOnlyList<ExerciseSeedDto> Exercises);
