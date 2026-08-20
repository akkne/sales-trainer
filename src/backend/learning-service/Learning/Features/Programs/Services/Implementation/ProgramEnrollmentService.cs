using Microsoft.EntityFrameworkCore;
using Sellevate.Learning.Common.Constants;
using Sellevate.Learning.Features.Programs.Models;
using Sellevate.Learning.Features.Programs.Services.Abstract;
using Sellevate.Learning.Infrastructure.Data;

namespace Sellevate.Learning.Features.Programs.Services.Implementation;

/// <summary>
/// Phase 40.17. Pins learners to programme snapshots and — only on their own request — moves them.
///
/// <para>
/// Read <see cref="EnrollAsync"/> and <see cref="SwitchAsync"/> together; the asymmetry between them
/// is the whole point of the block and is easy to "simplify" away. Enrollment is idempotent and
/// never moves an existing pin, so an administrator re-running "enroll everybody" after publishing a
/// new version puts newcomers on it and leaves everybody mid-course exactly where they were.
/// Switching is the learner's own act, names the version they agreed to, and is the only write in
/// the service that changes an existing pin.
/// </para>
/// </summary>
internal sealed class ProgramEnrollmentService(
    LearningDbContext databaseContext,
    IProgramVersionService programVersionService,
    ILogger<ProgramEnrollmentService> logger) : IProgramEnrollmentService
{
    /// <summary>
    /// Reports the learner's pinned programme, whether a newer published version exists, and what
    /// switching to it would change.
    ///
    /// <para>
    /// <b>Not enrolled is a normal answer, not an error.</b> The organization may have published no
    /// programme at all, in which case its people go on reading the live library exactly as they did
    /// before this phase; enrollment deliberately does not gate access to lessons (docs/DECISIONS.md,
    /// 2026-08-17). Callers must therefore treat <c>IsEnrolled: false</c> as a valid state rather
    /// than as "set-up incomplete".
    /// </para>
    ///
    /// <para>
    /// The pending diff is computed only when a switch is actually available, so the common
    /// steady-state call — pinned to the newest version — costs no diff query.
    /// </para>
    /// </summary>
    public async Task<MyProgramDto> GetMyProgramAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        ProgramEnrollment? enrollment;
        LatestPublished? latestPublished;

        await using (var tenantScope = await TenantTransactionScope.BeginReadAsync(databaseContext, cancellationToken))
        {
            enrollment = await databaseContext.ProgramEnrollments
                .AsNoTracking()
                .FirstOrDefaultAsync(candidate => candidate.UserId == userId, cancellationToken);

            latestPublished = await ReadLatestPublishedAsync(cancellationToken);
        }

        if (enrollment is null)
        {
            return new MyProgramDto(
                IsEnrolled: false,
                ProgramVersionId: null,
                ProgramVersionNumber: null,
                EnrolledAt: null,
                SwitchedAt: null,
                Items: [],
                LatestPublishedProgramVersionId: latestPublished?.Id,
                LatestPublishedProgramVersionNumber: latestPublished?.VersionNumber,
                SwitchAvailable: false,
                PendingDiff: null);
        }

        var pinnedVersion = await programVersionService.GetVersionAsync(enrollment.ProgramVersionId, cancellationToken);
        var switchAvailable = latestPublished is not null && latestPublished.Id != enrollment.ProgramVersionId;

        var pendingDiff = switchAvailable
            ? await programVersionService.GetDiffAsync(
                enrollment.ProgramVersionId, latestPublished!.Id, cancellationToken)
            : null;

        return new MyProgramDto(
            IsEnrolled: true,
            ProgramVersionId: enrollment.ProgramVersionId,
            ProgramVersionNumber: pinnedVersion?.VersionNumber,
            EnrolledAt: enrollment.EnrolledAt,
            SwitchedAt: enrollment.SwitchedAt,
            Items: pinnedVersion?.Items ?? [],
            LatestPublishedProgramVersionId: latestPublished?.Id,
            LatestPublishedProgramVersionNumber: latestPublished?.VersionNumber,
            SwitchAvailable: switchAvailable,
            PendingDiff: pendingDiff);
    }

    public async Task<IReadOnlyList<ProgramEnrollmentDto>> GetEnrollmentsAsync(
        CancellationToken cancellationToken = default)
    {
        await using var tenantScope = await TenantTransactionScope.BeginReadAsync(databaseContext, cancellationToken);

        return await BuildEnrollmentListQuery(databaseContext).ToListAsync(cancellationToken);
    }

    /// <summary>
    /// The enrollment list query, factored out so a test can check that Npgsql can translate it
    /// without needing a database (<c>ToQueryString()</c>).
    ///
    /// <para>
    /// <b>The ordering must stay on the entity, ahead of the projection.</b> Ordering by a member of
    /// the projected record reads more naturally and is untranslatable: it throws
    /// <see cref="InvalidOperationException"/> at execution time, which is how this endpoint came to
    /// answer 500 in production.
    /// </para>
    /// </summary>
    internal static IQueryable<ProgramEnrollmentDto> BuildEnrollmentListQuery(
        LearningDbContext databaseContext)
        => databaseContext.ProgramEnrollments
            .AsNoTracking()
            .OrderBy(enrollment => enrollment.EnrolledAt)
            .Join(
                databaseContext.ProgramVersions,
                enrollment => enrollment.ProgramVersionId,
                version => version.Id,
                (enrollment, version) => new ProgramEnrollmentDto(
                    enrollment.UserId,
                    enrollment.ProgramVersionId,
                    version.VersionNumber,
                    enrollment.PreviousProgramVersionId,
                    enrollment.EnrolledAt,
                    enrollment.SwitchedAt));

    /// <summary>
    /// Pins the learner to the newest published programme version, or returns their existing pin
    /// unchanged. Returns <c>null</c> when the organization has published no version to pin to.
    ///
    /// <para>
    /// <b>An existing enrollment is deliberately never moved onto the newest version.</b> An
    /// administrator repeating this call after a publish must not become a way to rearrange the
    /// programme of somebody already halfway through it; that move is the learner's own act and lives
    /// in <see cref="SwitchAsync"/>. The idempotence is what makes "enroll everybody" safe to re-run:
    /// newcomers land on the new version, everybody mid-course stays put.
    /// </para>
    /// </summary>
    public async Task<ProgramEnrollmentDto?> EnrollAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        await using var tenantScope = await TenantTransactionScope.BeginWriteAsync(databaseContext, cancellationToken);

        var latestPublished = await ReadLatestPublishedAsync(cancellationToken);
        if (latestPublished is null)
        {
            return null;
        }

        var existingEnrollment = await databaseContext.ProgramEnrollments
            .FirstOrDefaultAsync(candidate => candidate.UserId == userId, cancellationToken);

        if (existingEnrollment is not null)
        {
            var currentVersionNumber = await databaseContext.ProgramVersions
                .Where(version => version.Id == existingEnrollment.ProgramVersionId)
                .Select(version => version.VersionNumber)
                .FirstOrDefaultAsync(cancellationToken);

            await tenantScope.CommitAsync(cancellationToken);
            return ToDto(existingEnrollment, currentVersionNumber);
        }

        var enrollment = new ProgramEnrollment
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ProgramVersionId = latestPublished.Id,
            EnrolledAt = DateTime.UtcNow,
        };

        databaseContext.ProgramEnrollments.Add(enrollment);
        await databaseContext.SaveChangesAsync(cancellationToken);
        await tenantScope.CommitAsync(cancellationToken);

        logger.LogInformation(
            "Program enrollment created UserId={UserId} ProgramVersionId={ProgramVersionId} VersionNumber={VersionNumber}",
            userId, latestPublished.Id, latestPublished.VersionNumber);

        return ToDto(enrollment, latestPublished.VersionNumber);
    }

    /// <summary>
    /// Moves the learner's pin to <paramref name="targetProgramVersionId"/> at their own request,
    /// recording where they came from, and returns their refreshed programme.
    ///
    /// <para>
    /// <b>The target must be explicitly named and already published.</b> Naming the version is what
    /// makes the move consented rather than imposed, and the published check stops a learner being
    /// moved onto a draft an author is still editing. <c>null</c> means no move happened — not
    /// enrolled, already on that version, or the target is not published — and the caller maps all
    /// three to the same client response.
    /// </para>
    ///
    /// <para>
    /// The refreshed programme is read after the write transaction has committed, so the response
    /// reflects the new pin rather than the pre-switch snapshot.
    /// </para>
    /// </summary>
    public async Task<MyProgramDto?> SwitchAsync(
        Guid userId,
        Guid targetProgramVersionId,
        CancellationToken cancellationToken = default)
    {
        await using (var tenantScope = await TenantTransactionScope.BeginWriteAsync(databaseContext, cancellationToken))
        {
            var enrollment = await databaseContext.ProgramEnrollments
                .FirstOrDefaultAsync(candidate => candidate.UserId == userId, cancellationToken);
            if (enrollment is null || enrollment.ProgramVersionId == targetProgramVersionId)
            {
                return null;
            }

            var targetIsPublished = await databaseContext.ProgramVersions
                .AnyAsync(
                    version => version.Id == targetProgramVersionId
                               && version.Status == ProgramVersionStatuses.Published,
                    cancellationToken);
            if (!targetIsPublished)
            {
                return null;
            }

            enrollment.PreviousProgramVersionId = enrollment.ProgramVersionId;
            enrollment.ProgramVersionId = targetProgramVersionId;
            enrollment.SwitchedAt = DateTime.UtcNow;

            await databaseContext.SaveChangesAsync(cancellationToken);
            await tenantScope.CommitAsync(cancellationToken);

            logger.LogInformation(
                "Program enrollment switched UserId={UserId} FromProgramVersionId={FromProgramVersionId} ToProgramVersionId={ToProgramVersionId}",
                userId, enrollment.PreviousProgramVersionId, targetProgramVersionId);
        }

        return await GetMyProgramAsync(userId, cancellationToken);
    }

    private Task<LatestPublished?> ReadLatestPublishedAsync(CancellationToken cancellationToken)
        => databaseContext.ProgramVersions
            .AsNoTracking()
            .Where(version => version.Status == ProgramVersionStatuses.Published)
            .OrderByDescending(version => version.VersionNumber)
            .Select(version => new LatestPublished(version.Id, version.VersionNumber))
            .FirstOrDefaultAsync(cancellationToken);

    private static ProgramEnrollmentDto ToDto(ProgramEnrollment enrollment, int versionNumber)
        => new(
            enrollment.UserId,
            enrollment.ProgramVersionId,
            versionNumber,
            enrollment.PreviousProgramVersionId,
            enrollment.EnrolledAt,
            enrollment.SwitchedAt);

    private sealed record LatestPublished(Guid Id, int VersionNumber);
}
