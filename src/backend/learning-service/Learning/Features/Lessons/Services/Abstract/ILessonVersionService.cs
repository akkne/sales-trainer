using Sellevate.Learning.Features.Lessons.Models;

namespace Sellevate.Learning.Features.Lessons.Services.Abstract;

/// <summary>
/// Phase 40.15. The whole lifecycle of an immutable lesson version, in four operations. Every one
/// of them returns <see langword="null"/> when the lesson does not exist or is not visible to the
/// caller's organization — the tenancy layer already makes those two cases indistinguishable on
/// purpose, and the caller turns either into a 404.
/// </summary>
public interface ILessonVersionService
{
    Task<IReadOnlyList<LessonVersionSummaryDto>?> GetVersionsAsync(
        Guid lessonId,
        CancellationToken cancellationToken = default);

    Task<LessonVersionDto?> GetVersionAsync(
        Guid lessonId,
        Guid versionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Opens editing: returns the lesson's single mutable draft, creating it if there is none, and
    /// re-synchronising its snapshot with the lesson's current working rows either way.
    /// </summary>
    Task<LessonVersionDto?> EnsureDraftAsync(
        Guid lessonId,
        Guid? actorId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Freezes the current working rows as a published version. If their content hash equals that
    /// of the last published version, nothing is frozen and the existing version comes back with
    /// <c>CreatedNewVersion = false</c>.
    /// </summary>
    Task<PublishLessonVersionResultDto?> PublishAsync(
        Guid lessonId,
        bool isBreaking,
        Guid? actorId,
        CancellationToken cancellationToken = default);
}
