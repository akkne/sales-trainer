namespace Sellevate.Learning.Features.Lessons.Services.Abstract;

/// <summary>
/// Phase 40.16. Gives every lesson that has never been published a "version 1", so that the
/// historical progress backfill (<c>docs/TENANCY/sql/40.16_progress_version_backfill.sql</c>) has
/// something to point the existing attempts at.
///
/// <para>
/// This is the half of the migration that cannot be written in SQL. The snapshot's
/// <c>ContentHash</c> is a SHA-256 over the exact bytes <c>LessonSnapshotSerializer</c> emits, with
/// object keys in ordinal order; Postgres stores <c>jsonb</c> with its own key order and its own
/// whitespace rules, so a version minted by <c>jsonb_build_object</c> would carry a hash the service
/// never reproduces — and the next publish would mint a second, identical version, which is exactly
/// what <c>content_hash</c> exists to prevent (docs/TENANCY/CONTENT_MODEL.md §2.1).
/// </para>
/// </summary>
public interface ILessonVersionBackfill
{
    /// <summary>
    /// Idempotent: mints nothing when every visible lesson already has a published version, which
    /// is the case on every start after the first. Returns how many versions were created.
    /// </summary>
    Task<int> BackfillMissingInitialVersionsAsync(CancellationToken cancellationToken = default);
}
