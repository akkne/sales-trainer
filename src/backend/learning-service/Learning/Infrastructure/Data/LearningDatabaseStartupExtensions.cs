using Microsoft.EntityFrameworkCore;
using Sellevate.BuildingBlocks.Persistence;
using Sellevate.BuildingBlocks.Tenancy;
using Sellevate.Learning.Features.Lessons.Services.Abstract;

namespace Sellevate.Learning.Infrastructure.Data;

/// <summary>
/// Brings learning-db up to the current schema at process start, then runs the one data backfill that
/// cannot live inside a migration.
///
/// <para>
/// Phase 40.16. Startup has no request and therefore no organization, so the scope declares system
/// mode explicitly rather than running on a blank tenant context indistinguishable from a forgotten
/// one (docs/TENANCY/TENANCY.md §1.6) — the same shape gamification-service uses for its seeders.
/// </para>
///
/// <para>
/// The lesson-version backfill gives lessons that have never been published a version 1, so the
/// historical progress backfill has something to bind existing attempts to. It is idempotent and a
/// no-op on every start after the first, and it cannot be SQL inside a migration because the
/// snapshot's content hash is defined over bytes only <c>LessonSnapshotSerializer</c> produces (see
/// <see cref="ILessonVersionBackfill"/>).
/// </para>
/// </summary>
public static class LearningDatabaseStartupExtensions
{
    public static async Task MigrateAndBackfillAsync(
        this WebApplication application,
        IConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        using var serviceScope = application.Services.CreateScope();

        serviceScope.ServiceProvider.GetRequiredService<TenantContext>().EnterSystemMode();

        var startupLogger = serviceScope.ServiceProvider.GetRequiredService<ILogger<Program>>();

        await DatabaseMigrator.MigrateAsync<LearningDbContext>(
            serviceScope.ServiceProvider, configuration, startupLogger, cancellationToken: cancellationToken);

        var lessonVersionBackfill = serviceScope.ServiceProvider.GetRequiredService<ILessonVersionBackfill>();
        await lessonVersionBackfill.BackfillMissingInitialVersionsAsync(cancellationToken);
    }
}
