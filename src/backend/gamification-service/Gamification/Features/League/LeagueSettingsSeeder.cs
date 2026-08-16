using Microsoft.EntityFrameworkCore;
using Sellevate.Gamification.Infrastructure.Data;

namespace Sellevate.Gamification.Features.League;

/// <summary>
/// GA6(a): Seeds the singleton GamificationSettings row at startup so that read-path getters never
/// need to write. Idempotent — no-ops if the row already exists.
///
/// <para>
/// Phase 40.13 removed LeagueSettings from this seeder. That row became tenant-scoped, and startup
/// has no tenant: seeding it here would either have to invent an organization or run in system
/// mode and write a row belonging to nobody, which the RLS policy then hides from everybody. A
/// league period is per-organization state now, so it is created when an organization first needs
/// one — <c>LeagueService.GetSettingsAsync</c> already returns a correct unsaved default for an
/// organization with no row, so the read path still never writes.
/// </para>
/// </summary>
public sealed class LeagueSettingsSeeder(GamificationDbContext databaseContext)
{
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        await SeedGamificationSettingsAsync(cancellationToken);
    }

    private async Task SeedGamificationSettingsAsync(CancellationToken cancellationToken)
    {
        var exists = await databaseContext.GamificationSettings.AnyAsync(cancellationToken);
        if (exists)
        {
            return;
        }

        databaseContext.GamificationSettings.Add(new Features.Gamification.Models.GamificationSettings());

        try
        {
            await databaseContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Another startup instance seeded first — that is fine.
        }
    }
}
