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
    /// <summary>
    /// Every version of the lesson, newest number first, without their content bodies. At most one of
    /// them is a draft. An empty list means the lesson exists and has never been versioned, which is
    /// a different answer from <see langword="null"/>.
    /// </summary>
    Task<IReadOnlyList<LessonVersionSummaryDto>?> GetVersionsAsync(
        Guid lessonId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// One version with its frozen snapshot body. The lesson id is part of the lookup, so a version id
    /// belonging to a different lesson yields <see langword="null"/> rather than that other lesson's
    /// content.
    /// </summary>
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

    /// <summary>
    /// Phase 40.16. The id of the version an attempt on this lesson must be recorded against: the
    /// newest published one, or a freshly minted "version 1" when the lesson has never been
    /// published at all. Returns <see langword="null"/> only when the lesson does not exist or is
    /// not visible.
    ///
    /// <para>
    /// The minting branch exists because publishing is an administrator's act and, at the moment
    /// 40.16 ships, no administrator has ever performed it: 40.15 created the table and left every
    /// existing lesson with zero versions. An attempt on such a lesson has nothing to point at, and
    /// the alternative — leaving it unbound — is the bug this phase exists to close. The minted
    /// version is <c>IsBreaking = false</c> and has no author: it records the content as it already
    /// was, which is not a change and belongs to nobody.
    /// </para>
    ///
    /// <para>
    /// It deliberately does <b>not</b> mint a version when the live rows have drifted from the last
    /// published snapshot. An administrator who edits an exercise and does not publish has not made
    /// the edit historically visible yet, and minting on their behalf would stamp every such
    /// edit — including a fixed comma — as an unattributed content change, which is precisely the
    /// series-splitting on cosmetics that <c>is_breaking</c> exists to avoid
    /// (docs/TENANCY/CONTENT_MODEL.md §2.4). See docs/DECISIONS.md, 2026-08-17.
    /// </para>
    /// </summary>
    Task<Guid?> EnsurePublishedVersionIdAsync(
        Guid lessonId,
        CancellationToken cancellationToken = default);
}
